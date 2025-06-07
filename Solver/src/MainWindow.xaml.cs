using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.ObjectModel;
using Solver.Configuration;
using System.Windows.Shapes; // Required for Line
using System.Windows.Threading;
using System.Windows.Media.Effects; // Required for DropShadowEffect
using System.Collections.Specialized; // Required for CollectionChanged
using System.Threading; // Add this using statement for CancellationToken

using Path = System.IO.Path;

namespace Solver;

public partial class MainWindow : Window
{
    private readonly TextBox[,] _boardTextBoxes = new TextBox[8, 6];
    private bool _isSolverRunning;
    private readonly SolverEngine _solverEngine;
    private int _debugBoardIndex = 0;
    private List<string> _loadedDemoBoards = new();
    private bool _isBoardAlreadyClear = false;
    private List<WordPath> _lastDisplayedSolutionPaths = new();
    private WordPath? _currentlyHighlightedPath;

    private readonly ProgressTracker _progressTracker;
    private readonly ILogger _appLogger; // Single logger for the application
    private CancellationTokenSource _currentSolverCts; // To check for abort in finally block
    private bool _noSolutionFound = false;

    private ObservableCollection<IncludedWord> UserExcludedWords { get; set; } = new();
    private ObservableCollection<IncludedWord> UserIncludedWords { get; set; } = new();
    private ObservableCollection<DisplayableSolutionWord> DisplayableSolutionWords { get; set; } = new();

    public MainWindow()
    {
        InitializeComponent();
        InitializeBoard();
        ExcludedWordsListBox.ItemsSource = UserExcludedWords;
        IncludedWordsListBox.ItemsSource = UserIncludedWords;
        SolutionWordsListBox.ItemsSource = DisplayableSolutionWords;

        UserIncludedWords.CollectionChanged += UserInclusionExclusion_CollectionChanged;
        UserExcludedWords.CollectionChanged += UserInclusionExclusion_CollectionChanged;

        async Task ReportProgressAction(List<WordPath> solutionForDisplay, long wps)
        {
            // If there's a non-empty solution or we're getting progress updates
            if (solutionForDisplay.Any() || wps > 0)
            {
                // Update the WPS information in the status bar if it's appropriate
                TimeSpan totalElapsedTime = _progressTracker.GetElapsedTimeThisSolve();
                long totalWordsAttempted = _progressTracker.GetTotalWordsAttemptedThisSolve();
                double overallWps = (totalElapsedTime.TotalSeconds > 0) ? (totalWordsAttempted / totalElapsedTime.TotalSeconds) : 0;
                if (double.IsNaN(overallWps) || double.IsInfinity(overallWps))
                {
                    overallWps = 0;
                }

                // Only update status bar if not aborted/no solution found and we have valid data
                if (!(_currentSolverCts != null && _currentSolverCts.IsCancellationRequested) &&
                    !_noSolutionFound &&
                    (solutionForDisplay.Any() || wps > 0))
                {
                    await Dispatcher.InvokeAsync(() => { UpdateStatusBar($"WPS: {wps:F0} | WPS (All): {overallWps:F0}"); });
                }
            }

            // Don't update the UI if we've aborted or no solution was found
            if (_currentSolverCts == null || !_currentSolverCts.IsCancellationRequested)
            {
                // Only update DisplayableSolutionWords if we have a solution to display
                // and we're not in the "No Solution Found" state
                if (solutionForDisplay.Any() && !_noSolutionFound)
                {
                    _ = Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_currentSolverCts != null && _currentSolverCts.IsCancellationRequested)
                        {
                            return;
                        }
                        _lastDisplayedSolutionPaths = new(solutionForDisplay);
                        _currentlyHighlightedPath = null;
                        RedrawPathsWithHighlight();

                        var newSolutionWordPaths = solutionForDisplay;
                        var newSolutionWordsSet = newSolutionWordPaths.Select(wp => wp.Word).ToHashSet();

                        for (int i = DisplayableSolutionWords.Count - 1; i >= 0; i--)
                        {
                            if (!newSolutionWordsSet.Contains(DisplayableSolutionWords[i].Word))
                            {
                                DisplayableSolutionWords.RemoveAt(i);
                            }
                        }

                        for (int i = 0; i < newSolutionWordPaths.Count; i++)
                        {
                            var requiredPath = newSolutionWordPaths[i];
                            var existingDisplayWord = DisplayableSolutionWords.FirstOrDefault(dsw => dsw.Word == requiredPath.Word);

                            if (existingDisplayWord != null)
                            {
                                int currentIndex = DisplayableSolutionWords.IndexOf(existingDisplayWord);
                                if (currentIndex != i)
                                {
                                    DisplayableSolutionWords.Move(currentIndex, i);
                                }
                            }
                            else
                            {
                                DisplayableSolutionWords.Insert(i, new(requiredPath));
                            }
                        }

                        while (DisplayableSolutionWords.Count > newSolutionWordPaths.Count)
                        {
                            DisplayableSolutionWords.RemoveAt(DisplayableSolutionWords.Count - 1);
                        }

                        UpdateDisplayableWordStates();
                        UpdateIncludedWordsPathStatus();
                    }), DispatcherPriority.Background);
                }
            }
        }

        _progressTracker = new(ReportProgressAction);

#if DEBUG
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appFolderPath = Path.Combine(appDataPath, ConfigurationService.AppName);
        Directory.CreateDirectory(appFolderPath);
        
        string appLogFilePath = Path.Combine(appFolderPath, "app_debug.log");
        Logger loggerForDebug = new Logger();
        loggerForDebug.AddLogger(new StatusBarLogger(UpdateStatusBar));
        loggerForDebug.AddLogger(new FileLogger(appLogFilePath));
        _appLogger = loggerForDebug;
        _solverEngine = new(_appLogger); 
        System.Diagnostics.Debug.WriteLine($"[DEBUG] Application logging to: {appLogFilePath}");
#else
        Logger loggerForRelease = new Logger();
        loggerForRelease.AddLogger(new StatusBarLogger(UpdateStatusBar));
        _appLogger = loggerForRelease;
        _solverEngine = new(_appLogger);
#endif
    }

    // Helper method to update the HasPath status of all included words
    private void UpdateIncludedWordsPathStatus()
    {
        // Create a set of all words that have paths on the board
        var wordsWithPaths = new HashSet<string>(
            _lastDisplayedSolutionPaths.Select(wp => wp.Word),
            StringComparer.OrdinalIgnoreCase);

        // Update the HasPath property for each included word
        foreach (var includedWord in UserIncludedWords)
        {
            includedWord.HasPath = wordsWithPaths.Contains(includedWord.Word);
        }
    }

    private string GetDemoBoardsPath()
    {
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appFolderPath = Path.Combine(appDataPath, ConfigurationService.AppName);
        return Path.Combine(appFolderPath, App.ConfigService.Settings.DemoBoardsFileName);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadDemoBoards();

        try
        {
            _solverEngine.InitializeTrie();
            _appLogger.Log("Dictionary loaded successfully.");
        }
        catch (FileNotFoundException fnfEx)
        {
            _appLogger.LogError($"Dictionary file not found: {fnfEx.Message}. Please configure the path in Settings.");
        }
        catch (InvalidOperationException ioEx)
        {
            _appLogger.LogError($"Dictionary path not configured: {ioEx.Message}. Please configure the path in Settings.");
        }
        catch (Exception ex)
        {
            _appLogger.LogError($"Failed to load dictionary: {ex.Message}");
        }
    }

    private void LoadDemoBoards()
    {
        string demoBoardsFilePath = GetDemoBoardsPath();
        _loadedDemoBoards = new List<string>();

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(demoBoardsFilePath));

            if (!File.Exists(demoBoardsFilePath))
            {
                var defaultBoards = App.ConfigService.Settings.DefaultDemoBoards;
                if (defaultBoards != null && defaultBoards.Any())
                {
                    File.WriteAllLines(demoBoardsFilePath, defaultBoards.Where(b => !string.IsNullOrWhiteSpace(b) && b.Length == 48));
                    _appLogger.Log($"Created demo boards file with {defaultBoards.Count(b => !string.IsNullOrWhiteSpace(b) && b.Length == 48)} default boards.");
                }
                else
                {
                    File.WriteAllText(demoBoardsFilePath, string.Empty);
                    _appLogger.Log($"Created empty demo boards file: {App.ConfigService.Settings.DemoBoardsFileName}.");
                }
            }

            if (File.Exists(demoBoardsFilePath))
            {
                _appLogger.Log($"Loading demo boards from {demoBoardsFilePath}");
                var fileContent = File.ReadAllLines(demoBoardsFilePath);

                _loadedDemoBoards = fileContent
                                       .Where(line => !string.IsNullOrWhiteSpace(line) && line.Length == 48)
                                       .Select(line => line.Trim().ToLowerInvariant())
                                       .ToList();

                if (!_loadedDemoBoards.Any())
                {
                    _appLogger.Log($"Demo boards file ({App.ConfigService.Settings.DemoBoardsFileName}) is empty or contains no valid board strings.");
                }
                else
                {
                    _loadedDemoBoards.Reverse();
                    _debugBoardIndex = 0;
                    _appLogger.Log($"Loaded {_loadedDemoBoards.Count} demo boards from {App.ConfigService.Settings.DemoBoardsFileName}.");
                }
            }
        }
        catch (Exception ex)
        {
            _appLogger.LogError($"Error accessing demo boards file: {ex.Message}");
            _loadedDemoBoards = new List<string>();
        }
    }

    private void InitializeBoard()
    {
        Title = "Word Finder";
        BoardGrid.Children.Clear();
        PathOverlay.Children.Clear();
        DisplayableSolutionWords.Clear();

        double cellWidth = 30 + 2 * 2;
        double cellHeight = 30 + 2 * 2;
        double boardWidth = cellWidth * 6;
        double boardHeight = cellHeight * 8;

        PathOverlay.Width = boardWidth;
        PathOverlay.Height = boardHeight;

        for (int row = 0; row < 8; row++)
        {
            for (int col = 0; col < 6; col++)
            {
                int currentRow = row;
                int currentCol = col;

                var tb = new TextBox
                {
                    MaxLength = 1,
                    FontSize = 20,
                    Width = 30,
                    Height = 30,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Margin = new(2),
                    Background = (SolidColorBrush)Application.Current.Resources["TextBoxBackgroundColor"],
                    Foreground = (SolidColorBrush)Application.Current.Resources["TextForegroundColor"],
                    BorderBrush = (SolidColorBrush)Application.Current.Resources["BorderColorBrush"]
                };

                tb.PreviewTextInput += (sender, e) =>
                {
                    var textBox = sender as TextBox;
                    if (textBox == null)
                    {
                        return;
                    }

                    string input = e.Text.ToLower();
                    if (input.Length == 1 && char.IsLetter(input[0]))
                    {
                        textBox.Text = input.ToUpper();
                        textBox.CaretIndex = 1;
                    }
                    e.Handled = true;

                    int nextCol = (currentCol + 1) % 6;
                    int nextRow = currentRow + (currentCol + 1) / 6;

                    if (nextRow < 8)
                    {
                        _boardTextBoxes[nextRow, nextCol].Focus();
                    }
                };

                tb.GotFocus += (sender, e) =>
                {
                    var textBox = sender as TextBox;
                    if (textBox != null)
                    {
                        textBox.CaretIndex = textBox.Text.Length;
                    }
                };

                tb.PreviewKeyDown += (sender, e) =>
                {
                    var textBox = sender as TextBox;
                    if (textBox == null)
                    {
                        return;
                    }

                    switch (e.Key)
                    {
                        case Key.Back when textBox.CaretIndex == 0:
                        {
                            int prevCol = (currentCol - 1 + 6) % 6;
                            int prevRow = currentRow - (currentCol == 0 ? 1 : 0);

                            if (prevRow >= 0)
                            {
                                _boardTextBoxes[prevRow, prevCol].Focus();
                            }
                            e.Handled = true;
                            break;
                        }
                        case Key.Back:
                            textBox.Clear();
                            e.Handled = true;
                            break;
                        case Key.Delete:
                            textBox.Clear();
                            e.Handled = true;
                            break;
                        case Key.Right:
                        {
                            int nextCol = (currentCol + 1) % 6;
                            int nextRow = currentRow + (currentCol + 1) / 6;

                            if (nextRow < 8)
                            {
                                _boardTextBoxes[nextRow, nextCol].Focus();
                            }
                            e.Handled = true;
                            break;
                        }
                        case Key.Left when !string.IsNullOrEmpty(textBox.Text) && textBox.CaretIndex == textBox.Text.Length:
                            textBox.CaretIndex = 0;
                            e.Handled = true;
                            break;
                        case Key.Left:
                        {
                            int prevCol = (currentCol - 1 + 6) % 6;
                            int prevRow = currentRow - (currentCol == 0 ? 1 : 0);

                            if (prevRow >= 0)
                            {
                                _boardTextBoxes[prevRow, prevCol].Focus();
                            }
                            e.Handled = true;
                            break;
                        }
                        case Key.Up:
                        {
                            int prevRow = currentRow - 1;

                            if (prevRow >= 0)
                            {
                                _boardTextBoxes[prevRow, currentCol].Focus();
                            }
                            e.Handled = true;
                            break;
                        }
                        case Key.Down:
                        {
                            int nextRow = currentRow + 1;

                            if (nextRow < 8)
                            {
                                _boardTextBoxes[nextRow, currentCol].Focus();
                            }
                            e.Handled = true;
                            break;
                        }
                        default:
                            break;    
                    }
                };

                _boardTextBoxes[row, col] = tb;
                BoardGrid.Children.Add(tb);
            }
        }
        SetBoardEnabled(true);
    }

    private void SolveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isSolverRunning)
        {
            AbortSolver();
            return;
        }

        // Log a line here to indicate the current state of the cancellation token source
        _appLogger.Log($"Starting solve operation. Cancellation requested: {_currentSolverCts?.IsCancellationRequested ?? false}");

        _isBoardAlreadyClear = false;
        _currentSolverCts = new CancellationTokenSource();
        var cancellationToken = _currentSolverCts.Token;
        _noSolutionFound = false;

        var boardStringBuilder = new StringBuilder();
        bool boardIsValid = true;
        for (int row = 0; row < 8; row++)
        {
            for (int column = 0; column < 6; column++)
            {
                var textBox = _boardTextBoxes[row, column];
                if (textBox is null || string.IsNullOrWhiteSpace(textBox.Text) || !char.IsLetter(textBox.Text[0]))
                {
                    _appLogger.Log("Validation failed: All cells must be filled with a single letter (A-Z).");
                    boardIsValid = false;
                    break;
                }
                boardStringBuilder.Append(textBox.Text[0]);
            }
            if (!boardIsValid)
            {
                _currentSolverCts.Dispose();
                _currentSolverCts = null;
                return;
            }
        }

        if (!boardIsValid)
        {
            _currentSolverCts.Dispose();
            _currentSolverCts = null;
            return;
        }

        string boardStringRaw = boardStringBuilder.ToString().ToLowerInvariant();
        string demoBoardsFilePath = GetDemoBoardsPath();
        bool alreadyExists = _loadedDemoBoards.Contains(boardStringRaw, StringComparer.OrdinalIgnoreCase);

        if (!alreadyExists)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(demoBoardsFilePath));
                File.AppendAllText(demoBoardsFilePath, boardStringRaw + Environment.NewLine);
                _loadedDemoBoards.Insert(0, boardStringRaw);
                _debugBoardIndex = 0;
                _appLogger.Log($"Current board added to {App.ConfigService.Settings.DemoBoardsFileName}. Total: {_loadedDemoBoards.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to append board to demo boards file: {ex.Message}");
                _appLogger.LogError($"Error saving board to demo list.");
            }
        }

        char[,] board = Utils.CreateBoardFromString(boardStringRaw);

        SetBoardEnabled(false);
        ClearButton.IsEnabled = false;
        DebugButton.IsEnabled = false;
        SolveButton.Content = "Abort";
        _isSolverRunning = true;

        var knownWords = UserIncludedWords.ToList();
        _progressTracker.ResetForNewSolveAttempt();
        
        var solverTaskCts = _currentSolverCts;

        Task.Run(async () =>
        {
            try
            {
                if (solverTaskCts.IsCancellationRequested)
                {
                    return;
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    if (!solverTaskCts.IsCancellationRequested)
                    {
                        DisplayableSolutionWords.Clear();
                        PathOverlay.Children.Clear();
                        _appLogger.Log("Working... Please wait...");
                    }
                });

                if (solverTaskCts.IsCancellationRequested)
                {
                    return;
                }

                var solution = await _solverEngine.ExecuteAsync(board,
                                                             knownWords.Select(k => k.Word).ToList(),
                                                             _progressTracker,
                                                             UserExcludedWords.Select(e => e.Word).ToList(),
                                                             cancellationToken);

                // Check if no solution was found
                if (solution != null && !solution.IsSolved)
                {
                    _noSolutionFound = true;
                }
            }
            catch (OperationCanceledException)
            {
                await Dispatcher.InvokeAsync(() => _appLogger.Log("Solver operation was canceled during execution."));
            }
            catch (Exception ex)
            {
                if (!solverTaskCts.IsCancellationRequested)
                {
                    _ = Dispatcher.InvokeAsync(() => _appLogger.LogError($"An error occurred: {ex.Message}"));
                }
            }
            finally
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    SetBoardEnabled(true);
                    ClearButton.IsEnabled = true;
                    DebugButton.IsEnabled = true;
                    SolveButton.Content = "Solve";
                    _isSolverRunning = false;
                });
                solverTaskCts.Dispose();
                if (_currentSolverCts == solverTaskCts)
                {
                    _currentSolverCts = null;
                }
            }
        });
    }

    // Find an IncludedWord by string value
    private IncludedWord FindIncludedWord(ObservableCollection<IncludedWord> collection, string word)
    {
        return collection.FirstOrDefault(w => string.Equals(w.Word, word, StringComparison.OrdinalIgnoreCase));
    }

    // Add a word if it doesn't exist
    private void AddWordToCollection(ObservableCollection<IncludedWord> collection, string word, bool hasPath = true)
    {
        if (FindIncludedWord(collection, word) == null)
        {
            collection.Add(new IncludedWord(word, hasPath));
        }
    }

    // Remove a word by string value
    private bool RemoveWordFromCollection(ObservableCollection<IncludedWord> collection, string word)
    {
        var item = FindIncludedWord(collection, word);
        if (item != null)
        {
            return collection.Remove(item);
        }
        return false;
    }

    // Check if collection contains a word
    private bool ContainsWord(ObservableCollection<IncludedWord> collection, string word)
    {
        return FindIncludedWord(collection, word) != null;
    }

    private void SetBoardEnabled(bool isEnabled)
    {
        foreach (var textBox in _boardTextBoxes)
        {
            if (textBox == null)
            {
                continue;
            }

            textBox.IsEnabled = isEnabled;
            textBox.Background = isEnabled ? (SolidColorBrush)Application.Current.Resources["TextBoxBackgroundColor"] : (SolidColorBrush)Application.Current.Resources["TextBoxDisabledBackgroundColor"];
            textBox.Foreground = (SolidColorBrush)Application.Current.Resources["TextForegroundColor"];
            textBox.BorderBrush = (SolidColorBrush)Application.Current.Resources["BorderColorBrush"];
        }
    }

    private void AbortSolver()
    {
        if (_currentSolverCts is not null && !_currentSolverCts.IsCancellationRequested)
        {
            _currentSolverCts.Cancel();
        }
        _solverEngine.Abort();

        SetBoardEnabled(true);
        ClearButton.IsEnabled = true;
        DebugButton.IsEnabled = true;
        SolveButton.Content = "Solve";
        _isSolverRunning = false;

        _appLogger.Log("Solver aborted by user.");
    }

    internal void UpdateStatusBar(string message, bool isError = false)
    {
        Dispatcher.Invoke(() =>
        {
            StatusBarText.Text = message;
            StatusBarText.Foreground = isError ? Brushes.Red : (SolidColorBrush)Application.Current.Resources["StatusBarForegroundColor"];
        });
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        bool boardWasCleared = false;
        foreach (var textBox in _boardTextBoxes)
        {
            if (string.IsNullOrEmpty(textBox.Text))
            {
                continue;
            }

            textBox.Clear();
            boardWasCleared = true;
        }

        if (PathOverlay.Children.Count > 0)
        {
            PathOverlay.Children.Clear();
            boardWasCleared = true;
        }

        if (DisplayableSolutionWords.Any())
        {
            DisplayableSolutionWords.Clear();
            boardWasCleared = true;
        }

        if (boardWasCleared) 
        {
            _solverEngine.ClearPrePruningCache();
        }

        if (boardWasCleared || !_isBoardAlreadyClear)
        {
            _appLogger.Log("Board and solution display cleared. Click Clear again to clear Include/Exclude lists.");
            _isBoardAlreadyClear = true;
        }
        else
        {
            bool listsWereCleared = false;
            if (UserIncludedWords.Any())
            {
                UserIncludedWords.Clear();
                NewIncludedWordTextBox.Clear();
                listsWereCleared = true;
            }
            if (UserExcludedWords.Any())
            {
                UserExcludedWords.Clear();
                if (NewExcludedWordTextBox != null)
                {
                    NewExcludedWordTextBox.Clear();
                }

                listsWereCleared = true;
            }

            _appLogger.Log(listsWereCleared
                ? "Include/Exclude lists cleared."
                : "Board and Include/Exclude lists are already empty.");
            _isBoardAlreadyClear = false;
        }
    }

    private void DebugPopulateButton_Click(object sender, RoutedEventArgs e)
    {
        PopulateBoardWithDemoData();
        _solverEngine.ClearPrePruningCache();
    }

    private void PopulateBoardWithDemoData()
    {
        int rows = _boardTextBoxes.GetLength(0);
        int cols = _boardTextBoxes.GetLength(1);

        if (!_loadedDemoBoards.Any())
        {
            _appLogger.Log($"No demo boards available. Check {App.ConfigService.Settings.DemoBoardsFileName}.");
            return;
        }

        string currentDebugBoardString = _loadedDemoBoards.ElementAt(_debugBoardIndex);
        _debugBoardIndex = (_debugBoardIndex + 1) % _loadedDemoBoards.Count();

        if (currentDebugBoardString.Length != rows * cols)
        {
            _appLogger.LogError($"Demo board string '{currentDebugBoardString}' has incorrect length. Expected {rows * cols}, got {currentDebugBoardString.Length}.");
            return;
        }

        int index = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                _boardTextBoxes[row, col].Text = currentDebugBoardString[index].ToString().ToUpper();
                index++;
            }
        }

        DisplayableSolutionWords.Clear();
        PathOverlay.Children.Clear();
        UserIncludedWords.Clear();
        UserExcludedWords.Clear();
        NewIncludedWordTextBox.Clear();
        NewExcludedWordTextBox.Clear();

        _isBoardAlreadyClear = false;

        _appLogger.Log($"Populated with demo board: #{_debugBoardIndex}. Included/Excluded lists cleared.");
    }

    private Point GetCanvasCoordinates((int Row, int Col) position)
    {
        if (BoardGrid.Columns == 0 || BoardGrid.Rows == 0 || PathOverlay.ActualWidth == 0 || PathOverlay.ActualHeight == 0)
        {
            return new Point(0, 0);
        }
        var cellWidth = PathOverlay.ActualWidth / BoardGrid.Columns;
        var cellHeight = PathOverlay.ActualHeight / BoardGrid.Rows;

        return new(
            position.Col * cellWidth + cellWidth / 2,
            position.Row * cellHeight + cellHeight / 2
        );
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        double cellWidth = 30 + 2 * 2;
        double cellHeight = 30 + 2 * 2;
        PathOverlay.Width = cellWidth * 6;
        PathOverlay.Height = cellHeight * 8;

        if (_lastDisplayedSolutionPaths.Any())
        {
            RedrawPathsWithHighlight();
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow { Owner = this };
        AppTheme originalTheme = App.ConfigService.Settings.SelectedTheme;
        bool? result = settingsWindow.ShowDialog();

        if (result != true)
        {
            return;
        }

        bool themeChanged = App.ConfigService.Settings.SelectedTheme != originalTheme;

        if (themeChanged)
        {
            ((App)Application.Current).ApplyTheme(App.ConfigService.Settings.SelectedTheme);
            SetBoardEnabled(!_isSolverRunning);
        }
        
        _appLogger.Log("Settings saved. Attempting to reload dictionary...");
        try
        {
            _solverEngine.InitializeTrie();
            _appLogger.Log("Dictionary reloaded successfully with new settings.");
        }
        catch (FileNotFoundException fnfEx)
        {
            _appLogger.LogError($"Failed to reload dictionary: {fnfEx.Message}. Please check the path in settings.");
        }
        catch (InvalidOperationException ioEx)
        {
            _appLogger.LogError($"Failed to reload dictionary: {ioEx.Message}. Please configure the path in settings.");
        }
        catch (Exception ex)
        {
            _appLogger.LogError($"An error occurred while reloading the dictionary: {ex.Message}");
        }
    }

    private void SolutionWordsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement element && element.DataContext is DisplayableSolutionWord selectedDisplayableWord)
        {
            string selectedWord = selectedDisplayableWord.Word;
            SolutionDoubleClickAction behavior = App.ConfigService.Settings.DoubleClickBehavior;

            if (behavior == SolutionDoubleClickAction.AddToExcluded)
            {
                if (!ContainsWord(UserExcludedWords, selectedWord))
                {
                    AddWordToCollection(UserExcludedWords, selectedWord);
                    RemoveWordFromCollection(UserIncludedWords, selectedWord);
                    _appLogger.Log($"'{selectedWord}' added to excluded words list (double-click).");
                }
                else
                {
                    _appLogger.Log($"'{selectedWord}' is already in the excluded words list.");
                }
            }
            else
            {
                if (!ContainsWord(UserIncludedWords, selectedWord))
                {
                    AddWordToCollection(UserIncludedWords, selectedWord);
                    RemoveWordFromCollection(UserExcludedWords, selectedWord);
                    _appLogger.Log($"'{selectedWord}' added to included words list (double-click).");
                }
                else
                {
                    _appLogger.Log($"'{selectedWord}' is already in the included words list.");
                }
            }
        }
        else if (SolutionWordsListBox.SelectedItem is DisplayableSolutionWord directSelectedItem)
        {
            string selectedWord = directSelectedItem.Word;
            SolutionDoubleClickAction behavior = App.ConfigService.Settings.DoubleClickBehavior;

            if (behavior == SolutionDoubleClickAction.AddToExcluded)
            {
                if (!ContainsWord(UserExcludedWords, selectedWord))
                {
                    AddWordToCollection(UserExcludedWords, selectedWord);
                    RemoveWordFromCollection(UserIncludedWords, selectedWord);
                    _appLogger.Log($"'{selectedWord}' added to excluded words list (double-click).");
                }
                else
                {
                    _appLogger.Log($"'{selectedWord}' is already in the excluded words list.");
                }
            }
            else
            {
                if (!ContainsWord(UserIncludedWords, selectedWord))
                {
                    AddWordToCollection(UserIncludedWords, selectedWord);
                    RemoveWordFromCollection(UserExcludedWords, selectedWord);
                    _appLogger.Log($"'{selectedWord}' added to included words list (double-click).");
                }
                else
                {
                    _appLogger.Log($"'{selectedWord}' is already in the included words list.");
                }
            }
        }
    }

    private void SolutionIncludeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: DisplayableSolutionWord displayableWord })
        {
            return;
        }

        string word = displayableWord.Word;
        if (!ContainsWord(UserIncludedWords, word))
        {
            AddWordToCollection(UserIncludedWords, word);
            RemoveWordFromCollection(UserExcludedWords, word);
            _appLogger.Log($"'{word}' added to included words list.");
        }
        else
        {
            _appLogger.Log($"'{word}' is already in the included words list.");
        }
    }

    private void SolutionExcludeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: DisplayableSolutionWord displayableWord })
        {
            return;
        }

        string word = displayableWord.Word;
        if (!ContainsWord(UserExcludedWords, word))
        {
            AddWordToCollection(UserExcludedWords, word);
            RemoveWordFromCollection(UserIncludedWords, word);
            _appLogger.Log($"'{word}' added to excluded words list.");
        }
        else
        {
            _appLogger.Log($"'{word}' is already in the excluded words list.");
        }
    }

    private void ClearExcludedWordsButton_Click(object sender, RoutedEventArgs e)
    {
        if (UserExcludedWords.Any())
        {
            UserExcludedWords.Clear();
            _appLogger.Log("Excluded words list cleared.");
        }
        else
        {
            _appLogger.Log("Excluded words list is already empty.");
        }
    }

    private void ExcludedWordsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ExcludedWordsListBox.SelectedItem is IncludedWord selectedWord)
        {
            if (UserExcludedWords.Remove(selectedWord))
            {
                _appLogger.Log($"'{selectedWord.Word}' removed from excluded words list.");
            }
        }
    }

    private void AddIncludedWordButton_Click(object sender, RoutedEventArgs e)
    {
        string wordToAdd = NewIncludedWordTextBox.Text.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(wordToAdd))
        {
            _appLogger.Log("Please enter a word to include.");
            NewIncludedWordTextBox.Focus();
            return;
        }
        if (wordToAdd.Length < 4)
        {
            _appLogger.Log("Included words must be at least 4 characters long.");
            NewIncludedWordTextBox.Focus();
            return;
        }

        if (!ContainsWord(UserIncludedWords, wordToAdd))
        {
            bool pathExistsOnBoard = false;
            try
            {
                char[,] currentBoard = GetCurrentBoard();
                if (_solverEngine.IsInitialized)
                {
                    pathExistsOnBoard = _solverEngine.DoesWordHavePathOnBoard(currentBoard, wordToAdd);
                }
                else
                {
                    _appLogger.Log("Dictionary (Trie) not initialized. Cannot check path immediately.");
                }
            }
            catch (Exception ex)
            {
                _appLogger.LogError($"Error checking path for '{wordToAdd}': {ex.Message}");
            }

            var newIncludedWord = new IncludedWord(wordToAdd, pathExistsOnBoard);
            UserIncludedWords.Add(newIncludedWord);
            RemoveWordFromCollection(UserExcludedWords, wordToAdd); // Ensure it's not in the other list
            _appLogger.Log($"'{wordToAdd}' added to included words. Path on board: {pathExistsOnBoard}.");
            NewIncludedWordTextBox.Clear();
        }
        else
        {
            _appLogger.Log($"'{wordToAdd}' is already in the included words list.");
        }
        NewIncludedWordTextBox.Focus();
    }

    private void AddExcludedWordButton_Click(object sender, RoutedEventArgs e)
    {
        string wordToAdd = NewExcludedWordTextBox.Text.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(wordToAdd))
        {
            _appLogger.Log("Please enter a word to exclude.");
            NewExcludedWordTextBox.Focus();
            return;
        }
        if (wordToAdd.Length < 4)
        {
            _appLogger.Log("Excluded words must be at least 4 characters long.");
            NewExcludedWordTextBox.Focus();
            return;
        }

        if (!ContainsWord(UserExcludedWords, wordToAdd))
        {
            AddWordToCollection(UserExcludedWords, wordToAdd, false); // No need to check paths for excluded words
            RemoveWordFromCollection(UserIncludedWords, wordToAdd);
            _appLogger.Log($"'{wordToAdd}' added to excluded words list.");
            NewExcludedWordTextBox.Clear();
        }
        else
        {
            _appLogger.Log($"'{wordToAdd}' is already in the excluded words list.");
        }
        NewExcludedWordTextBox.Focus();
    }

    private void IncludedWordsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (IncludedWordsListBox.SelectedItem is IncludedWord selectedWord)
        {
            if (UserIncludedWords.Remove(selectedWord))
            {
                _appLogger.Log($"'{selectedWord.Word}' removed from included words list.");
            }
        }
    }

    private void ClearIncludedWordsButton_Click(object sender, RoutedEventArgs e)
    {
        if (UserIncludedWords.Any())
        {
            UserIncludedWords.Clear();
            _appLogger.Log("Included words list cleared.");
        }
        else
        {
            _appLogger.Log("Included words list is already empty.");
        }
    }

    private void SolutionWordItem_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_isSolverRunning)
        {
            return;
        }

        if (sender is ListBoxItem { DataContext: DisplayableSolutionWord displayableWord })
        {
            _currentlyHighlightedPath = displayableWord.FullPathData;
            RedrawPathsWithHighlight();
        }
    }

    private void SolutionWordItem_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_isSolverRunning)
        {
            return;
        }

        if (_currentlyHighlightedPath != null)
        {
            _currentlyHighlightedPath = null;
            RedrawPathsWithHighlight();
        }
    }

    private void RedrawPathsWithHighlight()
    {
        PathOverlay.Children.Clear();
        if (_lastDisplayedSolutionPaths == null || !_lastDisplayedSolutionPaths.Any())
        {
            return;
        }

        foreach (var wordPath in _lastDisplayedSolutionPaths)
        {
            bool isHighlighted = (wordPath == _currentlyHighlightedPath);
            var random = new Random(wordPath.Word.GetHashCode());
            
            SolidColorBrush brush;
            double strokeThickness = 4;
            DropShadowEffect glowEffect = null;

            if (isHighlighted)
            {
                brush = new(Colors.Gold);
                strokeThickness = 6;
                glowEffect = new()
                {
                    Color = Colors.Yellow,
                    ShadowDepth = 0,
                    BlurRadius = 10,
                    Opacity = 0.9
                };
            }
            else
            {
                byte lineOpacity = App.ConfigService.Settings.PathOpacityNormal;
                var color = Color.FromArgb(lineOpacity, (byte)random.Next(100, 200), (byte)random.Next(100, 200), (byte)random.Next(100, 200));
                brush = new(color);
            }

            for (int i = 1; i < wordPath.Positions.Count; i++)
            {
                var start = wordPath.Positions[i - 1];
                var end = wordPath.Positions[i];
                var startPoint = GetCanvasCoordinates(start);
                var endPoint = GetCanvasCoordinates(end);

                var line = new Line
                {
                    X1 = startPoint.X, Y1 = startPoint.Y,
                    X2 = endPoint.X, Y2 = endPoint.Y,
                    Stroke = brush,
                    StrokeThickness = strokeThickness,
                    Effect = isHighlighted ? glowEffect : null,
                };
                PathOverlay.Children.Add(line);
            }
        }
    }

    private void UserInclusionExclusion_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateDisplayableWordStates();
    }

    private void UpdateDisplayableWordStates()
    {
        if (DisplayableSolutionWords == null)
        {
            return;
        }

        foreach (var dsw in DisplayableSolutionWords)
        {
            if (dsw == null)
            {
                continue;
            }

            dsw.IsUserIncluded = ContainsWord(UserIncludedWords, dsw.Word);
            dsw.IsUserExcluded = ContainsWord(UserExcludedWords, dsw.Word);
        }
    }

    private char[,] GetCurrentBoard()
    {
        char[,] board = new char[8, 6];
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 6; c++)
            {
                var tb = _boardTextBoxes[r, c];
                if (tb != null && !string.IsNullOrWhiteSpace(tb.Text) && char.IsLetter(tb.Text[0]))
                {
                    board[r, c] = char.ToLowerInvariant(tb.Text[0]);
                }
                else
                {
                    board[r, c] = ' '; // Space ensures value won't be found in the Trie
                }
            }
        }
        return board;
    }
}

