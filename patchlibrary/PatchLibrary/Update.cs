using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PatchLibrary
{
    public enum EState
    {
        None,
        FetchingVersion,
        FetchingPatchData,
        FetchingPatch,
        InstallingPatch,
        UpToDate,
        Error
    }

    public class Updater
    {
        Task runningTask;

        public delegate void PropertyChangeEvent(object sender);

        protected EState _State = EState.None;
        #region property change event
        public EState State
        {
            get => _State;
            set
            {
                if (_State == value)
                {
                    return;
                }
                _State = value;
                StateOnChanged?.Invoke(this);
            }
        }
        public event PropertyChangeEvent StateOnChanged;
        #endregion

        protected long _CurrentVersion;
        #region property change event
        public long CurrentVersion
        {
            get => _CurrentVersion;
            set
            {
                if (_CurrentVersion == value)
                {
                    return;
                }
                _CurrentVersion = value;
                CurrentVersionOnChanged?.Invoke(this);
            }
        }
        public event PropertyChangeEvent CurrentVersionOnChanged;
        #endregion

        public bool DidPatch = false;

        protected long _LatestVersion;
        #region property change event
        public long LatestVersion
        {
            get => _LatestVersion;
            set
            {
                if (_LatestVersion == value)
                {
                    return;
                }
                _LatestVersion = value;
                LatestVersionOnChanged?.Invoke(this);
            }
        }
        public event PropertyChangeEvent LatestVersionOnChanged;
        #endregion


        protected int _PatchProgress;
        #region property change event
        public int PatchProgress
        {
            get => _PatchProgress;
            set
            {
                if (_PatchProgress == value)
                {
                    return;
                }
                _PatchProgress = value;
                PatchProgressOnChanged?.Invoke(this);
            }
        }
        public event PropertyChangeEvent PatchProgressOnChanged;
        #endregion


        protected int _PatchOverallProgress;
        #region property change event
        public int PatchOverallProgress
        {
            get => _PatchOverallProgress;
            set
            {
                if (_PatchOverallProgress == value)
                {
                    return;
                }
                _PatchOverallProgress = value;
                PatchOverallProgressOnChanged?.Invoke(this);
            }
        }
        public event PropertyChangeEvent PatchOverallProgressOnChanged;
        #endregion


        protected long _CurrentPatchBeingApplied;
        #region property change event
        public long CurrentPatchBeingApplied
        {
            get => _CurrentPatchBeingApplied;
            set
            {
                if (_CurrentPatchBeingApplied == value)
                {
                    return;
                }
                _CurrentPatchBeingApplied = value;
                CurrentPatchBeingAppliedOnChanged?.Invoke(this);
            }
        }
        public event PropertyChangeEvent CurrentPatchBeingAppliedOnChanged;
        #endregion


        protected string _CurrentPatchMessage;
        #region property change event
        public string CurrentPatchMessage
        {
            get => _CurrentPatchMessage;
            set
            {
                if (_CurrentPatchMessage == value)
                {
                    return;
                }
                _CurrentPatchMessage = value;
                CurrentPatchMessageOnChanged?.Invoke(this);
            }
        }
        public event PropertyChangeEvent CurrentPatchMessageOnChanged;
        #endregion


        protected string _Error;
        #region property change event
        public string Error
        {
            get => _Error;
            set
            {
                if (_Error == value)
                {
                    return;
                }
                _Error = value;
                ErrorOnChanged?.Invoke(this);
            }
        }
        public event PropertyChangeEvent ErrorOnChanged;
        #endregion


        DirectoryInfo mainFolder;
        string host;

        public Updater(DirectoryInfo mainFolder, string host)
        {
            this.mainFolder = mainFolder;
            this.host = host;
        }

        public IEnumerator<bool> RunUpdateEnumerable()
        {
            this.runningTask = RunUpdate();
            while(!this.runningTask.IsCompleted)
            {
                yield return false;
            }
        }

        public Task RunUpdate()
        {
            return Task.Run(async () => await InternalUpdate());
        }

        float Lerp(float firstFloat, float secondFloat, float by)
        {
            return firstFloat * (1 - by) + secondFloat * by;
        }

        int LerpInt(int firstInt, int secondInt, int by)
        {
            var e = (double)by / 100;
            return (int)(firstInt * (1 - e) + secondInt * e);
        }

        int GetProgress(long curr, long max)
        {
            return (int)(((double)curr * 100) / max);
        }

        long GetCurrentVersion()
        {
            string text = File.ReadAllText(mainFolder.File("version.dat").FullName);
            return long.Parse(text);
        }

        public async Task InternalUpdate()
        {
            CurrentVersion = GetCurrentVersion();

            try
            {
                State = EState.FetchingVersion;
                var latestVersion = await FetchLatestVersion();
                if(latestVersion == -1)
                {
                    return;
                }
                if(latestVersion == CurrentVersion)
                {
                    State = EState.UpToDate;
                    return;
                }
                DidPatch = true;

                LatestVersion = latestVersion;

                var patchFiles = await FetchPatches(CurrentVersion, LatestVersion);
                for (int i = 0; i < patchFiles.Length; i++)
                {
                    var start = GetProgress(i, patchFiles.Length);
                    var end = GetProgress(i + 1, patchFiles.Length);

                    this.CurrentPatchBeingApplied = i;

                    State = EState.FetchingPatch;
                    var patchFile = patchFiles[i];
                    var patchFileInfo = mainFolder.File("patch.patch");
                    await foreach (var (curr, max) in DownloadPatch(patchFileInfo, patchFile))
                    {
                        this.CurrentPatchMessage = $"Downloading Patch: {curr} / {max} bytes";
                        var progress = GetProgress(curr, max);
                        this.PatchProgress = progress;
                        this.PatchOverallProgress = LerpInt(start, end, progress / 2);

                        await Task.Delay(1000);
                    }



                    State = EState.InstallingPatch;
                    foreach (var (curr, max, msg, patchCurr, PatchTotal) in Patch.ApplyPatch(patchFileInfo, mainFolder))
                    {
                        this.CurrentPatchMessage = msg;
                        this.PatchProgress = GetProgress(curr, max);
                        this.PatchOverallProgress = LerpInt(start, end, GetProgress(patchCurr, PatchTotal)/2+50);

                        await Task.Delay(1000);
                    }
                }
                this.PatchProgress = 100;
                this.PatchOverallProgress = 100;
                State = EState.UpToDate;
            }
            catch (Exception ex)
            {
                State = EState.Error;
            }
        }

        public async Task<long> FetchLatestVersion()
        {
            using (HttpClient wc = new HttpClient())
            {
                try
                {
                    var data = await wc.GetStringAsync(host+"/latest_version");
                    return long.Parse(data);

                    //TODO better handling
                }
                catch (HttpRequestException e) {
                    State = EState.Error;
                }
                catch (InvalidOperationException e)
                {
                    State = EState.Error;
                }
                catch (TaskCanceledException e)
                {
                    State = EState.Error;
                }
                return -1;
            }
        }

        public async Task<string[]> FetchPatches(long from, long to)
        {
            using (HttpClient wc = new HttpClient())
            {
                try
                {
                    var data = await wc.GetStringAsync(host + $"/patches/{from}/{to}");
                    return data.Split("\n");

                    //TODO better handling
                }
                catch (HttpRequestException e)
                {
                    State = EState.Error;
                }
                catch (InvalidOperationException e)
                {
                    State = EState.Error;
                }
                catch (TaskCanceledException e)
                {
                    State = EState.Error;
                }
                return null;
            }
        }

        public async IAsyncEnumerable<(long,long)> DownloadPatch(FileInfo patchFile, string patch)
        {
            using (HttpClient wc = new HttpClient())
            {
                using (var response = await wc.GetAsync(host + $"/patch/{patch}"))
                using (var fileStream = File.Open(patchFile.FullName, FileMode.Create))
                using (BinaryWriter writer = new BinaryWriter(fileStream))
                {
                    response.Content.ReadAsStream();
                    var total = response.Content.Headers.ContentLength;
                    var contentStream  = response.Content.ReadAsStream();
                    if (total == null || total == 0)
                    {
                        total = -1;
                    }
                    foreach(var (curr, fileTotal) in Patch.WriteDirectFile(writer, contentStream, total.Value))
                    {
                        yield return (curr, fileTotal);
                    }
                }
                yield return (1,1);
            }
        }
    }
}
