using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DotNetEnv;

namespace PdfProcessor
{
    public partial class SettingsForm : Form
    {
        private TextBox? _inputFolderTextBox;
        private TextBox? _failedFolderTextBox;
        private TextBox? _pollingIntervalTextBox;
        private RadioButton? _intervalRadioButton;
        private RadioButton? _specificTimeRadioButton;
        private DateTimePicker? _specificTimePicker;
        private string? _originalInputFolder;
        private string? _originalFailedFolder;
        private int _originalPollingInterval;
        private string? _originalScheduleMode;
        private string? _originalSpecificTime = "00:00:00";

        public SettingsForm()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        private void InitializeComponent()
        {
            this.Text = "PDF Processor Settings";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ClientSize = new Size(500, 350);

            // Input Folder Label
            var inputLabel = new Label
            {
                Text = "Input Folder:",
                Location = new Point(20, 20),
                Size = new Size(100, 23)
            };
            this.Controls.Add(inputLabel);

            // Input Folder TextBox
            _inputFolderTextBox = new TextBox
            {
                Location = new Point(120, 20),
                Size = new Size(300, 23)
            };
            this.Controls.Add(_inputFolderTextBox);

            // Input Folder Browse Button
            var inputBrowseButton = new Button
            {
                Text = "Browse...",
                Location = new Point(430, 18),
                Size = new Size(50, 27)
            };
            inputBrowseButton.Click += InputBrowseButton_Click;
            this.Controls.Add(inputBrowseButton);

            // Failed Folder Label
            var failedLabel = new Label
            {
                Text = "Failed Folder:",
                Location = new Point(20, 60),
                Size = new Size(100, 23)
            };
            this.Controls.Add(failedLabel);

            // Failed Folder TextBox
            _failedFolderTextBox = new TextBox
            {
                Location = new Point(120, 60),
                Size = new Size(300, 23)
            };
            this.Controls.Add(_failedFolderTextBox);

            // Failed Folder Browse Button
            var failedBrowseButton = new Button
            {
                Text = "Browse...",
                Location = new Point(430, 58),
                Size = new Size(50, 27)
            };
            failedBrowseButton.Click += FailedBrowseButton_Click;
            this.Controls.Add(failedBrowseButton);

            // Schedule Mode Label
            var scheduleModeLabel = new Label
            {
                Text = "Schedule Mode:",
                Location = new Point(20, 100),
                Size = new Size(100, 23)
            };
            this.Controls.Add(scheduleModeLabel);

            // Interval Radio Button
            _intervalRadioButton = new RadioButton
            {
                Text = "Interval",
                Location = new Point(120, 100),
                Size = new Size(80, 23),
                Checked = true
            };
            _intervalRadioButton.CheckedChanged += ScheduleMode_CheckedChanged;
            this.Controls.Add(_intervalRadioButton);

            // Specific Time Radio Button
            _specificTimeRadioButton = new RadioButton
            {
                Text = "Specific Time",
                Location = new Point(210, 100),
                Size = new Size(100, 23)
            };
            _specificTimeRadioButton.CheckedChanged += ScheduleMode_CheckedChanged;
            this.Controls.Add(_specificTimeRadioButton);

            // Polling Interval Label
            var pollingLabel = new Label
            {
                Text = "Polling Interval (seconds):",
                Location = new Point(20, 130),
                Size = new Size(160, 23)
            };
            this.Controls.Add(pollingLabel);

            // Polling Interval TextBox
            _pollingIntervalTextBox = new TextBox
            {
                Location = new Point(190, 130),
                Size = new Size(100, 23)
            };
            this.Controls.Add(_pollingIntervalTextBox);

            // Specific Time Label
            var specificTimeLabel = new Label
            {
                Text = "Specific Time (24h):",
                Location = new Point(20, 160),
                Size = new Size(140, 23)
            };
            this.Controls.Add(specificTimeLabel);

            // Specific Time DateTimePicker
            _specificTimePicker = new DateTimePicker
            {
                Location = new Point(170, 160),
                Size = new Size(120, 23),
                Format = DateTimePickerFormat.Time,
                ShowUpDown = true,
                Enabled = false,
                CustomFormat = "HH:mm:ss"
            };
            this.Controls.Add(_specificTimePicker);

            // Save Button
            var saveButton = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.OK,
                Location = new Point(300, 270),
                Size = new Size(80, 30)
            };
            saveButton.Click += SaveButton_Click;
            this.Controls.Add(saveButton);

            // Cancel Button
            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(400, 270),
                Size = new Size(80, 30)
            };
            this.Controls.Add(cancelButton);

            // Set AcceptButton and CancelButton
            this.AcceptButton = saveButton;
            this.CancelButton = cancelButton;
        }

        private void LoadCurrentSettings()
        {
            try
            {
                Env.Load();
                _originalInputFolder = Env.GetString("PUBLIC_FOLDER_URL") ?? "C:\\Document";
                _originalFailedFolder = Env.GetString("FAILED_FOLDER_URL") ?? "Failed";
                _originalPollingInterval = int.TryParse(Env.GetString("POLLING_INTERVAL_SECONDS"), out var interval) ? interval : 60;
                _originalScheduleMode = Env.GetString("SCHEDULE_MODE") ?? "INTERVAL";
                _originalSpecificTime = Env.GetString("SPECIFIC_TIME") ?? "00:00:00";
            }
            catch
            {
                _originalInputFolder = "C:\\Document";
                _originalFailedFolder = "Failed";
                _originalPollingInterval = 60;
                _originalScheduleMode = "INTERVAL";
                _originalSpecificTime = "00:00:00";
            }

            if (_inputFolderTextBox != null)
            {
                _inputFolderTextBox.Text = _originalInputFolder;
            }
            if (_failedFolderTextBox != null)
            {
                _failedFolderTextBox.Text = _originalFailedFolder;
            }
            if (_pollingIntervalTextBox != null)
            {
                _pollingIntervalTextBox.Text = _originalPollingInterval.ToString();
            }
            if (_intervalRadioButton != null && _specificTimeRadioButton != null)
            {
                if (_originalScheduleMode == "SPECIFIC_TIME")
                {
                    _specificTimeRadioButton.Checked = true;
                }
                else
                {
                    _intervalRadioButton.Checked = true;
                }
            }
            if (_specificTimePicker != null)
            {
                if (TimeSpan.TryParse(_originalSpecificTime, out var time))
                {
                    _specificTimePicker.Value = DateTime.Today.Add(time);
                }
                else
                {
                    _specificTimePicker.Value = DateTime.Today;
                }
            }
        }

        private void InputBrowseButton_Click(object? sender, EventArgs e)
        {
            using var folderBrowser = new FolderBrowserDialog
            {
                Description = "Select Input Folder",
                ShowNewFolderButton = true
            };

            if (_inputFolderTextBox != null && Directory.Exists(_inputFolderTextBox.Text))
            {
                folderBrowser.SelectedPath = _inputFolderTextBox.Text;
            }

            if (folderBrowser.ShowDialog() == DialogResult.OK)
            {
                _inputFolderTextBox!.Text = folderBrowser.SelectedPath;
            }
        }

        private void FailedBrowseButton_Click(object? sender, EventArgs e)
        {
            using var folderBrowser = new FolderBrowserDialog
            {
                Description = "Select Failed Folder",
                ShowNewFolderButton = true
            };

            if (_failedFolderTextBox != null && Directory.Exists(_failedFolderTextBox.Text))
            {
                folderBrowser.SelectedPath = _failedFolderTextBox.Text;
            }

            if (folderBrowser.ShowDialog() == DialogResult.OK)
            {
                _failedFolderTextBox!.Text = folderBrowser.SelectedPath;
            }
        }

        private void SaveButton_Click(object? sender, EventArgs e)
        {
            var inputFolder = _inputFolderTextBox?.Text ?? "Input";
            var failedFolder = _failedFolderTextBox?.Text ?? "Failed";
            var pollingIntervalText = _pollingIntervalTextBox?.Text ?? "60";
            var scheduleMode = _intervalRadioButton?.Checked == true ? "INTERVAL" : "SPECIFIC_TIME";
            var specificTime = _specificTimePicker?.Value.ToString("HH:mm:ss") ?? "00:00:00";

            // Validate polling interval if interval mode is selected
            if (scheduleMode == "INTERVAL")
            {
                if (!int.TryParse(pollingIntervalText, out var pollingInterval) || pollingInterval < 1)
                {
                    MessageBox.Show("Polling interval must be a positive integer (seconds).", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.DialogResult = DialogResult.None;
                    return;
                }
            }

            // Validate folders are not the same
            var normalizedInputFolder = Path.GetFullPath(inputFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedFailedFolder = Path.GetFullPath(failedFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(normalizedInputFolder, normalizedFailedFolder, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Input folder and Failed folder cannot be the same. This would cause an infinite processing loop.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.DialogResult = DialogResult.None;
                return;
            }

            // Validate folders
            if (!Directory.Exists(inputFolder))
            {
                try
                {
                    Directory.CreateDirectory(inputFolder);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Cannot create input folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.DialogResult = DialogResult.None;
                    return;
                }
            }

            if (!Directory.Exists(failedFolder))
            {
                try
                {
                    Directory.CreateDirectory(failedFolder);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Cannot create failed folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    this.DialogResult = DialogResult.None;
                    return;
                }
            }

            // Save to .env file
            try
            {
                var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
                var envContent = $"PUBLIC_FOLDER_URL={inputFolder}{Environment.NewLine}FAILED_FOLDER_URL={failedFolder}{Environment.NewLine}SCHEDULE_MODE={scheduleMode}{Environment.NewLine}POLLING_INTERVAL_SECONDS={pollingIntervalText}{Environment.NewLine}SPECIFIC_TIME={specificTime}";
                File.WriteAllText(envPath, envContent);

                // Reload environment
                Env.Load();

                MessageBox.Show("Settings saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save settings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.DialogResult = DialogResult.None;
            }
        }

        private void ScheduleMode_CheckedChanged(object? sender, EventArgs e)
        {
            if (_intervalRadioButton != null && _specificTimeRadioButton != null && _pollingIntervalTextBox != null && _specificTimePicker != null)
            {
                if (_intervalRadioButton.Checked)
                {
                    _pollingIntervalTextBox.Enabled = true;
                    _specificTimePicker.Enabled = false;
                }
                else if (_specificTimeRadioButton.Checked)
                {
                    _pollingIntervalTextBox.Enabled = false;
                    _specificTimePicker.Enabled = true;
                }
            }
        }

        public string? GetInputFolder() => _inputFolderTextBox?.Text;
        public string? GetFailedFolder() => _failedFolderTextBox?.Text;
    }
}
