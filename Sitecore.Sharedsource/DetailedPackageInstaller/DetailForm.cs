﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using log4net;
using log4net.Appender;
using log4net.Repository.Hierarchy;
using log4net.spi;
using Sitecore.Configuration;
using Sitecore.Install;
using Sitecore.Reflection;
using Sitecore.Shell.Applications.Install.Dialogs.InstallPackage;
using Sitecore.Web.UI.HtmlControls;
using Sitecore.Web.UI.Sheer;

namespace Sitecore.Sharedsource.DetailedPackageInstaller
{
    public class DetailForm : InstallPackageForm
    {
        #region Private members

        private const int SleepTime = 500;

        private const string InnerPackageFileName = "package.zip";
        private const string PackageEntryItemPrefix = "items";

        private const string Info = "INFO";
        private const string Warning = "WARN";
        private const string Error = "ERROR";

        private const string JobStartedInstall = "Job started: Install";
        private const string InstallingPackage = "Installing package:";
        private const string InstallingItem = "Installing item:";
        private const string JobEndedInstall = "Job ended: Install";

        private const string JobStartedSecurityInstall = "Job started: InstallSecurity";
        private const string InstallingSecurity = "Installing security from package:";
        private const string JobEndedInstallSecurity = "Job ended: InstallSecurity";

        private const string JobStartedIndexUpdate = "Job started: Index_Update";
        private const string JobEndedIndexUpdate = "Job ended: Index_Update";

        #endregion

        #region Fields

        protected Checkbox DisableIndexing;
        protected Edit CurrentLogFilePath;
        protected Edit LogStartingPointEdit;
        protected Edit InstalledItemCountEdit;
        protected Edit TotalItemCountEdit;
        protected Edit MonitorLogEdit;
        protected Edit TrimLogContentsEdit;
        protected Edit InitialLoggingLevelEdit;

        #endregion

        #region Field Properties

        public long LogStartingPoint
        {
            get
            {
                return !string.IsNullOrEmpty(LogStartingPointEdit.Value) ? long.Parse(LogStartingPointEdit.Value) : 0;
            }
            set { LogStartingPointEdit.Value = value.ToString(CultureInfo.InvariantCulture); }
        }

        public int InstalledItemCount
        {
            get
            {
                var count = !string.IsNullOrEmpty(InstalledItemCountEdit.Value) ? int.Parse(InstalledItemCountEdit.Value) : 0;
                var total = TotalItemCount;
                return count <= total ? count : total;
            }
            set { InstalledItemCountEdit.Value = value.ToString(CultureInfo.InvariantCulture); }
        }

        public int TotalItemCount
        {
            get
            {
                return !string.IsNullOrEmpty(TotalItemCountEdit.Value) ? int.Parse(TotalItemCountEdit.Value) : 0;
            }
            set { TotalItemCountEdit.Value = value.ToString(CultureInfo.InvariantCulture); }
        }

        public bool MonitorLog
        {
            get
            {
                return !string.IsNullOrEmpty(MonitorLogEdit.Value) && bool.Parse(MonitorLogEdit.Value);
            }
            set { MonitorLogEdit.Value = value.ToString(CultureInfo.InvariantCulture); }
        }

        public bool TrimLogContents
        {
            get
            {
                return !string.IsNullOrEmpty(TrimLogContentsEdit.Value) && bool.Parse(TrimLogContentsEdit.Value);
            }
            set { TrimLogContentsEdit.Value = value.ToString(CultureInfo.InvariantCulture); }
        }

        #endregion

        #region Overrides

        protected override void OnNext(object sender, EventArgs formEventArgs)
        {
            base.OnNext(sender, formEventArgs);

            if (Active == "Installing")
            {
                MonitorLog = true;
                TrimLogContents = true;

                GetItemCountFromPackageFile();

                if (!HasInfoLoggingEnabled())
                {
                    EnableInfoLogging();
                }

                if (DisableIndexing.Checked)
                {
                    Settings.Indexing.Enabled = false;
                }

                GetCurrentLogPosition();
                SetInstallingPackageStatus(true);
                Context.ClientPage.ClientResponse.Eval("window.SharedSource.InstallPackage.CheckStatus()");
            }
            else
            {
                ResetLoggingLevel();
                MonitorLog = false;
            }
        }

        protected override void EndWizard()
        {
            base.EndWizard();

            ResetLoggingLevel();
            MonitorLog = false;

            if (!DisableIndexing.Checked) return;
            DisableIndexing.Checked = false;
            Settings.Indexing.Enabled = true;
        }

        #endregion

        #region Package Reader

        private void GetItemCountFromPackageFile()
        {
            int itemCount;
            var filename = Installer.GetFilename(PackageFile.Value);

            using (var archive = ZipFile.OpenRead(filename))
            {
                var packageEntry = archive.Entries.FirstOrDefault(e => e.FullName == InnerPackageFileName);
                if (packageEntry == null)
                {
                    return;
                }

                using (var packageStream = packageEntry.Open())
                {
                    using (var package = new ZipArchive(packageStream, ZipArchiveMode.Read))
                    {
                        itemCount = package.Entries.Count(e => e.FullName.StartsWith(PackageEntryItemPrefix));
                    }
                }
            }

            TotalItemCount = itemCount;
        }

        #endregion

        #region Log Reader

        private static bool HasInfoLoggingEnabled()
        {
            return ((Hierarchy) LogManager.GetLoggerRepository()).Root.IsEnabledFor(Level.INFO);
        }

        private void EnableInfoLogging()
        {
            var heirarchy = ((Hierarchy) LogManager.GetLoggerRepository());

            InitialLoggingLevelEdit.Value = heirarchy.Root.Level.ToString();

            heirarchy.Root.Level = Level.INFO;
        }

        private void ResetLoggingLevel()
        {
            if (string.IsNullOrEmpty(InitialLoggingLevelEdit.Value))
            {
                return;
            }

            var heirarchy = ((Hierarchy)LogManager.GetLoggerRepository());

            var levelValue = heirarchy.LevelMap[InitialLoggingLevelEdit.Value];

            heirarchy.Root.Level = levelValue;
        }

        private string GetRootLogFilePath()
        {
            var rootAppender = ((Hierarchy)LogManager.GetLoggerRepository())
                                                     .Root.Appenders.OfType<FileAppender>()
                                                     .FirstOrDefault();

            var path = rootAppender != null ? rootAppender.File : string.Empty;
            if (CurrentLogFilePath.Value == path) return path;

            LogStartingPoint = 0;
            CurrentLogFilePath.Value = path;

            return path;
        }

        private void GetCurrentLogPosition()
        {
            using (var reader = new LogReader(GetRootLogFilePath()))
            {
                LogStartingPoint = reader.GetFileLength();
            }
        }

        [HandleMessage("installer:CheckLogProgress")]
        private void CheckLogProgress(Message message)
        {
            string messageContent;
            long logStartingPoint;
            using (var reader = new LogReader(GetRootLogFilePath()))
            {
                messageContent = reader.GetUpdates(LogStartingPoint, out logStartingPoint);
            }
            LogStartingPoint = logStartingPoint;

            var messageList = CleanMessageContent(messageContent);

            DisplayLogMessages(messageList);
        }

        #endregion

        #region Messaging

        private List<string> CleanMessageContent(string message)
        {
            List<string> result;
            var split = message.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();

            if (TrimLogContents)
            {
                result = split.Where(line =>
                    (line.Contains(Info) &&
                        (line.Contains(JobStartedInstall) || line.Contains(InstallingPackage) || line.Contains(InstallingItem) || line.Contains(JobEndedInstall) ||
                         line.Contains(JobStartedSecurityInstall) || line.Contains(InstallingSecurity) || line.Contains(JobEndedInstallSecurity) ||
                         line.Contains(JobStartedIndexUpdate) || line.Contains(JobEndedIndexUpdate))) ||
                    line.Contains(Warning) || line.Contains(Error))
                    .ToList();
            }
            else
            {
                result = split.ToList();
            }

            return result;
        }

        private void DisplayLogMessages(List<string> messageContent)
        {
            if (!messageContent.Any()) return;

            IndicateStatus(messageContent);

            UpdateMessage(messageContent);

            var newlyInstalledCount = messageContent.Count(m => m.Contains(InstallingItem));

            if (newlyInstalledCount <= 0) return;

            var installedItemCount = InstalledItemCount + newlyInstalledCount;
            InstalledItemCount = installedItemCount;

            if (installedItemCount >= TotalItemCount)
            {
                SetInstallingPackageStatus(false);
            }

            UpdateProgress(((decimal)installedItemCount / TotalItemCount));
        }

        private void IndicateStatus(List<string> messageContent)
        {
            if (!messageContent.Any()) return;

            #region Install
            if (messageContent.Any(m => m.Contains(JobStartedInstall) || m.Contains(InstallingPackage)))
            {
                UpdateProgress((decimal) 0.0);
                SetInstallingPackageStatus(true);
            }
            if (messageContent.Any(m => m.Contains(JobEndedInstall)))
            {
                UpdateProgress((decimal) 1.0);
                SetInstallingPackageStatus(false);
            }
            #endregion
            #region Security
            if (messageContent.Any(m => m.Contains(JobStartedSecurityInstall) || m.Contains(InstallingSecurity)))
            {
                SetInstallingSecurityStatus(true);
            }
            if (messageContent.Any(m => m.Contains(JobEndedInstallSecurity)))
            {
                SetInstallingSecurityStatus(false);
            }
            #endregion
            #region Index
            if (messageContent.Any(m => m.Contains(JobStartedIndexUpdate)))
            {
                SetIndexUpdateStatus(true);
            }
            if (messageContent.Any(m => m.Contains(JobEndedIndexUpdate)))
            {
                SetIndexUpdateStatus(false);
            }
            #endregion
        }

        protected void UpdateProgress(decimal percentage)
        {
            var progress = $"{percentage:0%}";

            var countMessage = $"{InstalledItemCount} of {TotalItemCount} Item{(TotalItemCount == 1 ? "" : "s")} Installed ({percentage.ToString("P0")})";

            Context.ClientPage.ClientResponse.Eval($"window.SharedSource.InstallPackage.Progress('{progress}')");
            Context.ClientPage.ClientResponse.Eval($"window.SharedSource.InstallPackage.UpdateCountMessage('{countMessage}')");
        }

        protected void SetInstallingPackageStatus(bool active)
        {
            Context.ClientPage.ClientResponse.Eval($"window.SharedSource.InstallPackage.SetStatus('InstallingStatus', {(active ? "true" : "false")})");
        }

        protected void SetInstallingSecurityStatus(bool active)
        {
            Context.ClientPage.ClientResponse.Eval($"window.SharedSource.InstallPackage.SetStatus('SecurityStatus', {(active ? "true" : "false")})");
        }

        protected void SetIndexUpdateStatus(bool active)
        {
            Context.ClientPage.ClientResponse.Eval($"window.SharedSource.InstallPackage.SetStatus('IndexStatus', {(active ? "true" : "false")})");
        }

        protected void UpdateMessage(List<string> messageContent)
        {
            var message = "";
            foreach (var line in messageContent)
            {
                var lineClass = "";
                if (line.Contains(Info))
                {
                    lineClass = "info";
                }
                else if (line.Contains(Warning))
                {
                    lineClass = "warn";
                }
                else if (line.Contains(Error))
                {
                    lineClass = "error";
                }
                message = $"{message}<li class=\"{lineClass}\">{line}</li>";
            }
            Context.ClientPage.ClientResponse.Eval($"window.SharedSource.InstallPackage.UpdateLogMessage('{message}')");
        }

        #endregion

        #region Private Base Methods
        // ReSharper disable UnusedMember.Local

        [HandleMessage("installer:setTaskId")]
        private void OnSetTaskId(Message message)
        {
            var obj = this as InstallPackageForm;
            ReflectionUtil.CallMethod(typeof(InstallPackageForm), obj, "SetTaskID", true, true, new object[] { message });
        }

        [HandleMessage("installer:commitingFiles")]
        private void OnCommittingFiles(Message message)
        {
            var obj = this as InstallPackageForm;
            ReflectionUtil.CallMethod(typeof(InstallPackageForm), obj, "OnCommittingFiles", true, true, new object[] { message });
        }

        // ReSharper restore UnusedMember.Local
        #endregion
    }
}