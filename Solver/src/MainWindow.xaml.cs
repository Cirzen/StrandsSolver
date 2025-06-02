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

    private ObservableCollection<string> UserExcludedWords { get; set; } = new();
    private ObservableCollection<string> UserIncludedWords { get; set; } = new();
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

        async Task ReportProgressAction(List<WordPath> solutionForDisplay, long wps, Dictionary<(int, int), int> heatMap)
        {
            TimeSpan totalElapsedTime = _progressTracker.GetElapsedTimeThisSolve();
            long totalWordsAttempted = _progressTracker.GetTotalWordsAttemptedThisSolve();
            double overallWps = (totalElapsedTime.TotalSeconds > 0) ? (totalWordsAttempted / totalElapsedTime.TotalSeconds) : 0;
            if (double.IsNaN(overallWps) || double.IsInfinity(overallWps))
            {
                overallWps = 0;
            }

            await Dispatcher.InvokeAsync(() => { UpdateStatusBar($"WPS: {wps:F0} | WPS (All): {overallWps:F0}"); });

            _ = Dispatcher.BeginInvoke(new Action(() =>
            {
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
            }), DispatcherPriority.Background);
        }

        _progressTracker = new(ReportProgressAction);

#if DEBUG
        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appFolderPath = Path.Combine(appDataPath, ConfigurationService.AppName);
        Directory.CreateDirectory(appFolderPath);
        
        string appLogFilePath = Path.Combine(appFolderPath, "app_debug.log"); // Single log file
        Logger loggerForDebug = new Logger();
        loggerForDebug.AddLogger(new StatusBarLogger(UpdateStatusBar));
        loggerForDebug.AddLogger(new FileLogger(appLogFilePath));
        _appLogger = loggerForDebug;
        _solverEngine = new(_appLogger); // Pass the same logger to SolverEngine
        System.Diagnostics.Debug.WriteLine($"[DEBUG] Application logging to: {appLogFilePath}");
#else
        Logger loggerForRelease = new Logger();
        loggerForRelease.AddLogger(new StatusBarLogger(UpdateStatusBar));
        _appLogger = loggerForRelease;
        _solverEngine = new(_appLogger); // Pass the same logger to SolverEngine
#endif
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

                foreach (var line in fileContent)
                {
                    _appLogger.Log($"Demo board string {line} length: {line.Length}");
                }
                
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

        _isBoardAlreadyClear = false;

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
                break;
            }
        }

        if (!boardIsValid)
        {
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

        Task.Run(async () =>
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    DisplayableSolutionWords.Clear();
                    PathOverlay.Children.Clear();
                    _appLogger.Log("Working... Please wait...");
                });
                await _solverEngine.ExecuteAsync(board, knownWords, _progressTracker, UserExcludedWords.ToList());
            }
            catch (Exception ex)
            {
                _ = Dispatcher.InvokeAsync(() => _appLogger.LogError($"An error occurred: {ex.Message}"));
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
            }
        });
    }

    private void SetBoardEnabled(bool isEnabled)
    {
        foreach (var textBox in _boardTextBoxes)
        {
            if (textBox == null) continue;
            textBox.IsEnabled = isEnabled;
            textBox.Background = isEnabled ? (SolidColorBrush)Application.Current.Resources["TextBoxBackgroundColor"] : (SolidColorBrush)Application.Current.Resources["TextBoxDisabledBackgroundColor"];
            textBox.Foreground = (SolidColorBrush)Application.Current.Resources["TextForegroundColor"];
            textBox.BorderBrush = (SolidColorBrush)Application.Current.Resources["BorderColorBrush"];
        }
    }

    private void AbortSolver()
    {
        _solverEngine.Abort();

        SetBoardEnabled(true);
        ClearButton.IsEnabled = true;
        DebugButton.IsEnabled = true;
        SolveButton.Content = "Solve";
        _isSolverRunning = false;

        _appLogger.Log("Solver aborted.");
    }

    internal void UpdateStatusBar(string message, bool isError = false)
    {
        Dispatcher.Invoke(() =>
        {
            StatusBarText.Text = message;
            StatusBarText.Foreground = isError ? Brushes.Red : (SolidColorBrush)Application.Current.Resources["StatusBarForegroundColor"];
        });
    }

    private void ClearStatusBar()
    {
        Dispatcher.Invoke(() =>
        {
            StatusBarText.Text = string.Empty;
            StatusBarText.Foreground = Brushes.Black;
        });
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        bool boardWasActuallyCleared = false;
        foreach (var textBox in _boardTextBoxes)
        {
            if (string.IsNullOrEmpty(textBox.Text))
            {
                continue;
            }

            textBox.Clear();
            boardWasActuallyCleared = true;
        }

        if (PathOverlay.Children.Count > 0)
        {
            PathOverlay.Children.Clear();
            boardWasActuallyCleared = true;
        }

        if (DisplayableSolutionWords.Any())
        {
            DisplayableSolutionWords.Clear();
            boardWasActuallyCleared = true;
        }

        if (boardWasActuallyCleared) 
        {
            _solverEngine.ClearPrePruningCache();
        }

        if (boardWasActuallyCleared || !_isBoardAlreadyClear)
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
        PopulateBoardWithDebugData();
        _solverEngine.ClearPrePruningCache();
    }

    private void PopulateBoardWithDebugData()
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

    private void DrawPaths(List<WordPath> paths)
    {
        PathOverlay.Children.Clear();
        if (_lastDisplayedSolutionPaths == null || !_lastDisplayedSolutionPaths.Any())
        {
            return;
        }

        foreach (var wordPath in paths)
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

    private Point GetCanvasCoordinates((int Row, int Col) position)
    {
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
                if (!UserExcludedWords.Contains(selectedWord))
                {
                    UserExcludedWords.Add(selectedWord);
                    UserIncludedWords.Remove(selectedWord);
                    _appLogger.Log($"'{selectedWord}' added to excluded words list (double-click).");
                }
                else
                {
                    _appLogger.Log($"'{selectedWord}' is already in the excluded words list.");
                }
            }
            else
            {
                if (!UserIncludedWords.Contains(selectedWord))
                {
                    UserIncludedWords.Add(selectedWord);
                    UserExcludedWords.Remove(selectedWord);
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
                if (!UserExcludedWords.Contains(selectedWord))
                {
                    UserExcludedWords.Add(selectedWord);
                    UserIncludedWords.Remove(selectedWord);
                    _appLogger.Log($"'{selectedWord}' added to excluded words list (double-click).");
                }
                else
                {
                    _appLogger.Log($"'{selectedWord}' is already in the excluded words list.");
                }
            }
            else
            {
                if (!UserIncludedWords.Contains(selectedWord))
                {
                    UserIncludedWords.Add(selectedWord);
                    UserExcludedWords.Remove(selectedWord);
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
        if (!UserIncludedWords.Contains(word))
        {
            UserIncludedWords.Add(word);
            UserExcludedWords.Remove(word);
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
        if (!UserExcludedWords.Contains(word))
        {
            UserExcludedWords.Add(word);
            UserIncludedWords.Remove(word);
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
        if (ExcludedWordsListBox.SelectedItem is string selectedWordToRemove)
        {
            if (UserExcludedWords.Remove(selectedWordToRemove))
            {
                _appLogger.Log($"'{selectedWordToRemove}' removed from excluded words list.");
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

        if (!UserIncludedWords.Contains(wordToAdd))
        {
            UserIncludedWords.Add(wordToAdd);
            UserExcludedWords.Remove(wordToAdd);
            _appLogger.Log($"'{wordToAdd}' added to included words list.");
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

        if (!UserExcludedWords.Contains(wordToAdd))
        {
            UserExcludedWords.Add(wordToAdd);
            UserIncludedWords.Remove(wordToAdd);
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
        if (IncludedWordsListBox.SelectedItem is string selectedWordToRemove)
        {
            if (UserIncludedWords.Remove(selectedWordToRemove))
            {
                _appLogger.Log($"'{selectedWordToRemove}' removed from included words list.");
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

            dsw.IsUserIncluded = UserIncludedWords.Contains(dsw.Word);
            dsw.IsUserExcluded = UserExcludedWords.Contains(dsw.Word);
        }
    }
}

