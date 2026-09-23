using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using UltimateStartPage.Core.Models;
using UltimateStartPage.Core.Services;
using WinForms = System.Windows.Forms;

namespace UltimateStartPage.Dialogs
{
    /// <summary>Add / edit / pin a link. The title follows the target until the user types their own.</summary>
    public partial class LinkEditorDialog
    {
        /// <summary>How long typing must pause before the target is looked up on disk.</summary>
        private static readonly TimeSpan DiskCheckDelay = TimeSpan.FromMilliseconds(300);

        private readonly Func<string, LinkKind> _resolveKind;
        private readonly DispatcherTimer _diskCheckTimer;
        private int _diskCheck;
        private (string Target, LinkKind Kind)? _checkedOnDisk;
        private bool _titleEditedByUser;
        private bool _settingTitle;

        public LinkEditorDialog(LinkEditRequest request, Func<string, LinkKind> resolveKind)
        {
            _resolveKind = resolveKind ?? throw new ArgumentNullException(nameof(resolveKind));
            _diskCheckTimer = new DispatcherTimer { Interval = DiskCheckDelay };
            _diskCheckTimer.Tick += OnDiskCheckTimerTick;
            Closed += (_, __) => _diskCheckTimer.Stop();

            InitializeComponent();

            Title = request.DialogTitle;
            SectionBox.ItemsSource = request.SectionTitles;
            SectionBox.SelectedIndex = request.SectionTitles.Count > 0 ? request.SelectedSectionIndex : -1;
            SectionBox.IsEnabled = request.SectionTitles.Count > 1;

            _titleEditedByUser = !string.IsNullOrWhiteSpace(request.Title);
            SetTitle(request.Title);
            TargetBox.Text = request.Target;
            DescriptionBox.Text = request.Description ?? string.Empty;
            TargetBox.SelectAll();
        }

        public LinkEditResult? Result { get; private set; }

        private string Target => TargetBox.Text.Trim().Trim('"');

        /// <summary>
        /// Runs on every keystroke, so it only looks at the text. The disk (which may be an unreachable network share)
        /// is checked on the thread pool once typing pauses; see <see cref="CheckOnDiskAsync"/>.
        /// </summary>
        private void OnTargetChanged(object sender, TextChangedEventArgs e)
        {
            _diskCheckTimer.Stop();
            _diskCheck++; // any check already running is now for old text

            var target = Target;
            if (string.IsNullOrWhiteSpace(target))
            {
                KindText.Text = string.Empty;
                return;
            }

            var kind = LinkKindDetector.Detect(target);
            ShowKind(target, kind, foundOnDisk: true);

            if (kind != LinkKind.Url)
            {
                _diskCheckTimer.Start();
            }
        }

        private void OnDiskCheckTimerTick(object sender, EventArgs e)
        {
            _diskCheckTimer.Stop();
            _ = CheckOnDiskAsync(Target);
        }

        /// <summary>Works out the real kind (a folder called "My.Repo" is still a folder) and whether the target exists.</summary>
        private async Task CheckOnDiskAsync(string target)
        {
            var check = ++_diskCheck;
            LinkKind kind;
            bool found;

            try
            {
                (kind, found) = await Task.Run(() =>
                {
                    var resolved = _resolveKind(target);
                    return (resolved, File.Exists(target) || Directory.Exists(target));
                });
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is NotSupportedException || ex is UnauthorizedAccessException)
            {
                return; // not a usable path; the text-based hint stays
            }

            // Resumes on the dialog's thread. Drop the answer if the text changed (or the dialog closed) meanwhile.
            if (check != _diskCheck || !IsLoaded || target != Target)
            {
                return;
            }

            _checkedOnDisk = (target, kind);
            ShowKind(target, kind, found);
        }

        private void ShowKind(string target, LinkKind kind, bool foundOnDisk)
        {
            KindText.Text = foundOnDisk ? $"Opens as: {kind}" : $"Opens as: {kind}  (not found on this machine)";

            if (!_titleEditedByUser)
            {
                SetTitle(LinkKindDetector.SuggestTitle(target, kind));
            }
        }

        private void OnTitleChanged(object sender, TextChangedEventArgs e)
        {
            if (!_settingTitle)
            {
                _titleEditedByUser = !string.IsNullOrWhiteSpace(TitleBox.Text);
            }
        }

        private void SetTitle(string title)
        {
            _settingTitle = true;
            TitleBox.Text = title;
            _settingTitle = false;
        }

        private void OnBrowseFile(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Choose a solution, project or file",
                Filter = "Solutions and projects|*.sln;*.slnx;*.slnf;*.csproj;*.vbproj;*.fsproj;*.vcxproj;*.sqlproj;*.esproj;*.pyproj;*.njsproj|All files|*.*",
                CheckFileExists = true,
            };

            if (TryGetExistingDirectory(out var initial))
            {
                dialog.InitialDirectory = initial;
            }

            if (dialog.ShowDialog(this) == true)
            {
                TargetBox.Text = dialog.FileName;
            }
        }

        private void OnBrowseFolder(object sender, RoutedEventArgs e)
        {
            using (var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Choose a folder to open with File > Open > Folder",
                ShowNewFolderButton = false,
            })
            {
                if (TryGetExistingDirectory(out var initial))
                {
                    dialog.SelectedPath = initial;
                }

                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    TargetBox.Text = dialog.SelectedPath;
                }
            }
        }

        private bool TryGetExistingDirectory(out string directory)
        {
            directory = string.Empty;
            var target = Target;

            if (string.IsNullOrWhiteSpace(target) || LinkKindDetector.IsWebUrl(target))
            {
                return false;
            }

            try
            {
                directory = Directory.Exists(target) ? target : Path.GetDirectoryName(target) ?? string.Empty;
                return Directory.Exists(directory);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            var target = Target;
            if (string.IsNullOrWhiteSpace(target))
            {
                TargetBox.Focus();
                return;
            }

            // Use the disk check if it finished for this text; otherwise go by the text rather than block here. A
            // folder that looks like a file still opens as a folder, because the launcher checks again when opening.
            var kind = _checkedOnDisk?.Target == target ? _checkedOnDisk.Value.Kind : LinkKindDetector.Detect(target);
            var title = string.IsNullOrWhiteSpace(TitleBox.Text) ? LinkKindDetector.SuggestTitle(target, kind) : TitleBox.Text.Trim();
            var description = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim();

            Result = new LinkEditResult(title, target, kind, description, SectionBox.SelectedIndex);
            DialogResult = true;
        }
    }
}
