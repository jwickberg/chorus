﻿using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Chorus.UI.Misc;
using Chorus.VcsDrivers.Mercurial;
using L10NSharp;

namespace Chorus.UI.Clone
{
	public partial class GetCloneFromInternetDialog : Form, ICloneSourceDialog
	{
		private readonly GetCloneFromInternetModel _model;
		private readonly BackgroundWorker _backgroundWorker;
		private enum State { AskingUserForURL, MakingClone, Success, Error,Cancelled}
		private Button _downloadButton;
		private State _state;
		private ServerSettingsControl _serverSettingsControl;

		public GetCloneFromInternetDialog(string parentDirectoryToPutCloneIn)
			:this(new GetCloneFromInternetModel(parentDirectoryToPutCloneIn))
		{
		}

		public GetCloneFromInternetDialog(GetCloneFromInternetModel model)
		{
			_model = model;
//#if !MONO
			Font = SystemFonts.MessageBoxFont;
//#endif
			InitializeComponent();

			Font = SystemFonts.MessageBoxFont;

			_backgroundWorker = new BackgroundWorker();
			_backgroundWorker.WorkerSupportsCancellation = true;
			_backgroundWorker.RunWorkerCompleted += _backgroundWorker_RunWorkerCompleted;
			_backgroundWorker.DoWork += _backgroundWorker_DoWork;

			_logBox.ShowCopyToClipboardMenuItem = true;
			_logBox.ShowDetailsMenuItem = true;
			_logBox.ShowDiagnosticsMenuItem = true;
			_logBox.ShowFontMenuItem = true;


			_model.AddProgress(_statusProgress);
			_statusProgress.Text = "";
			_statusProgress.Visible = false;
			_model.AddMessageProgress(_logBox);
			_model.ProgressIndicator = _progressBar;
			_model.UIContext = SynchronizationContext.Current;

			_serverSettingsControl = new ServerSettingsControl(){Model=_model};
			_serverSettingsControl.TabIndex = 0;
			_serverSettingsControl.Anchor = (AnchorStyles.Top | AnchorStyles.Left);
			Controls.Add(_serverSettingsControl);

			_downloadButton = new Button();
			_downloadButton.Text = "Download"; //<- Localize this
			_downloadButton.Top = _okButton.Top;
			_downloadButton.Left = _okButton.Left;
			_downloadButton.Click += OnDownloadClick;
			Controls.Add(_downloadButton);
			_okButton.TabIndex = 90;
			_cancelButton.TabIndex = 91;

			_fixSettingsButton.Left = _cancelButton.Left;

			_logBox.GetDiagnosticsMethod = (progress) =>
											{
												var hg = new HgRepository(PathToNewlyClonedFolder, progress);
												hg.GetDiagnosticInformationForRemoteProject(progress, ThreadSafeUrl);
											};

		}

		private void _backgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			if (_statusProgress.ErrorEncountered)
			{
				UpdateDisplay(State.Error);
				_model.CleanUpAfterErrorOrCancel();
				_statusProgress.Reset();
			}
			else if (_model.CancelRequested)
			{
				_model.CancelRequested = false;
				UpdateDisplay(State.Cancelled);
				_model.CleanUpAfterErrorOrCancel();
				_statusProgress.Reset();
			}
			else
			{
				UpdateDisplay(_model.SetRepositoryAddress() ? State.Success : State.Error);
			}
		}

		void _backgroundWorker_DoWork(object sender, DoWorkEventArgs e)
		{
			_model.DoClone();
		}

		private void UpdateDisplay(State newState)
		{
			_state = newState;
			_fixSettingsButton.Visible = false;
			switch (_state)
			{
				case State.AskingUserForURL:
					_statusLabel.Visible = false;
					_statusImage.Visible   =false;
					_logBox.Visible = false;
					_okButton.Visible = false;
					_progressBar.Visible = false;
					_downloadButton.Visible = true;
					_cancelButton.Enabled = true;
					_cancelButton.Visible = true;
					_cancelTaskButton.Visible = false;
					_statusProgress.Visible = false;
					_statusProgress.Text = "";
					_serverSettingsControl.DisplayUpdated += ServerSettingsControlOnDisplayUpdated;

					break;
				case State.MakingClone:
					_serverSettingsControl.DisplayUpdated -= ServerSettingsControlOnDisplayUpdated;
					_progressBar.Focus();
					_progressBar.Select();
					_downloadButton.Visible = false;
					_statusImage.Visible = false;
					_progressBar.Visible = true;
					_progressBar.Style = ProgressBarStyle.Marquee;
#if MONO
					_progressBar.MarqueeAnimationSpeed = 3000;
#else
					_progressBar.MarqueeAnimationSpeed = 50;
#endif
					_statusLabel.Visible = true;
					_statusLabel.Text = LocalizationManager.GetString("Messages.GettingProject", "Getting project...");
					_statusLabel.Left = _progressBar.Left;
					_statusProgress.Left = _progressBar.Left;
					_logBox.Visible = true;
					_cancelTaskButton.Visible = true;
					_cancelButton.Visible = false;
					_statusProgress.Visible = true;
					break;
				case State.Success:
					_serverSettingsControl.DisplayUpdated -= ServerSettingsControlOnDisplayUpdated;
					_cancelTaskButton.Visible = false;
					_statusLabel.Visible = true;
					_statusLabel.Text = LocalizationManager.GetString("Messages.Done", "Done.");
					_progressBar.Visible = false;
					_downloadButton.Visible = false;
					_statusLabel.Left = _statusImage.Right + 10;
					_statusImage.Visible = true;
					_statusImage.ImageKey=LocalizationManager.GetString("Messages.Success", "Success");
					_statusLabel.Text = string.Format(LocalizationManager.GetString("Messages.Finished", "Finished"));
					_okButton.Visible = true;
					_cancelButton.Visible = false;
					_logBox.Visible = true;
					_statusProgress.Visible = false;
					break;
				case State.Error:
					_serverSettingsControl.DisplayUpdated -= ServerSettingsControlOnDisplayUpdated;
					_fixSettingsButton.Visible = true;
					_fixSettingsButton.Focus();
					_cancelButton.Visible = true;
#if !MONO
					_cancelButton.Text = LocalizationManager.GetString("Common.Cancel", "&Cancel");
#endif
					//_cancelButton.Select();
					_cancelTaskButton.Visible = false;
					_statusLabel.Visible = true;
					_statusLabel.Text = LocalizationManager.GetString("Messages.Failed", "Failed.");
					_progressBar.Visible = false;
					_downloadButton.Visible = false;
					_statusLabel.Left = _statusImage.Right + 10;
					_statusImage.ImageKey = LocalizationManager.GetString("Common.Error", "Error");
					_statusImage.Visible = true;
					_statusProgress.Visible = false;
					break;
				case State.Cancelled:
					_serverSettingsControl.DisplayUpdated -= ServerSettingsControlOnDisplayUpdated;
					_cancelButton.Visible = true;
					_cancelButton.Text = LocalizationManager.GetString("Common.Close", "&Close");
					_cancelButton.Select();
					_cancelTaskButton.Visible = false;
					_statusLabel.Visible = true;
					_statusLabel.Text = LocalizationManager.GetString("Messages.Cancelled", "Cancelled.");
					_progressBar.Visible = false;
					_downloadButton.Visible = false;
					_statusLabel.Left =  _progressBar.Left;
					_statusImage.Visible = false;
					_statusProgress.Visible = false;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			SetupAcceptButton();
			_serverSettingsControl.Visible = _downloadButton.Visible;
		}

		private void SetupAcceptButton()
		{
			if (_downloadButton.Visible)
				AcceptButton = _downloadButton;
			else if (_okButton.Visible)
				AcceptButton = _okButton;
			else
				AcceptButton = null;
		}

		private void ServerSettingsControlOnDisplayUpdated(object sender, EventArgs eventArgs)
		{
			EnableDownloadButtonByProjectId();
			//_targetFolderControl.UpdateDisplay();
		}

		private void EnableDownloadButtonByProjectId()
		{
			if (String.IsNullOrEmpty(_model.ProjectId))
				_downloadButton.Enabled = false;
			else
				_downloadButton.Enabled = true;
		}

		private void OnLoad(object sender, EventArgs e)
		{
			UpdateDisplay(State.AskingUserForURL);
			_logBox.BackColor = this.BackColor;
			EnableDownloadButtonByProjectId();
		}

		private void _okButton_Click(object sender, EventArgs e)
		{
		   DialogResult = DialogResult.OK;
			Close();
		}


		/// <summary>
		/// After a successful clone, this will have the path to the folder that we just copied to the computer
		/// </summary>
		public string PathToNewlyClonedFolder
		{
			get { return _model.TargetDestination; }
		}

		/// <summary>
		/// **** Currently this is not implemented on this class
		/// </summary>
		public void SetFilePatternWhichMustBeFoundInHgDataFolder(string pattern)
		{
			//TODO
			//no don't do throw. doing it means client need special code for each clone method
			//  throw new NotImplementedException();

		}

		private void _cancelButton_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.Cancel;
				Close();
		}

		private void OnDownloadClick(object sender, EventArgs e)
		{
			lock (this)
			{
				_logBox.Clear();
				if(_backgroundWorker.IsBusy)
					return;
				UpdateDisplay(State.MakingClone);
				ThreadSafeUrl = _model.URL;
				//_backgroundWorker.RunWorkerAsync(new object[] { ThreadSafeUrl, PathToNewProject, _progress });
				_backgroundWorker.RunWorkerAsync(new object[0]);
			}
		}


		public string ThreadSafeUrl
		{
			get;
			set;
		}

		private void button2_Click(object sender, EventArgs e)
		{
			if (_state == State.MakingClone)
			{
				lock (this)
				{
					if (!_backgroundWorker.IsBusy)
						return;

					_backgroundWorker.CancelAsync();//the hg call will know nothing of this
					_model.CancelRequested = true; //but it will be monitoring this
				}
			}
		}

		private void _logBox_Load(object sender, EventArgs e)
		{
		}

		private void _fixSettingsButton_Click(object sender, EventArgs e)
		{
			//_statusProgress.Reset();
			_model.Click_FixSettingsButton();
			UpdateDisplay(State.AskingUserForURL);
		}

		private void GetCloneFromInternetDialog_BackColorChanged(object sender, EventArgs e)
		{
			_logBox.BackColor  =this.BackColor;
		}
	}
}
