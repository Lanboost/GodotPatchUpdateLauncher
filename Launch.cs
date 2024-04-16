using System;
using System.Net.Http;
using System.Threading.Tasks;
using static Godot.WebSocketPeer;

public partial class Launch : Godot.Control
{
    public enum EState
    {
        None,
        FetchingVersion,
        FetchingPatchData,
        FetchingPatch,
        InstallingPatch,
        UpToDate
    }

    [Godot.Export]

    public Godot.Button loginButton;

    public string base_host = "http://www.google.com/";

    Task runningTask;
    public override void _Ready()
    {
        base._Ready();
        loginButton.Pressed += LoginButton_Pressed;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if(runningTask != null)
        {
            if(runningTask.IsCompleted)
            {
                runningTask = null;
            }
        }
    }

    private void LoginButton_Pressed()
    {
        StartFetchingLatestVersion();
    }

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

    protected string _CurrentVersion;
    #region property change event
    public string CurrentVersion
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


    protected string _LatestVersion;
    #region property change event
    public string LatestVersion
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





    public class Result<T>
    {
        public bool Success;
        public string Error;
        public T Value;

        public static Result<T> Fail(string message)
        {
            var r = new Result<T>();
            r.Success = false;
            r.Error = message;
            return r;
        }

        public static Result<T> Ok(T va)
        {
            var r = new Result<T>();
            r.Success = true;
            r.Value = va;
            return r;
        }
    }

    public void StartFetchingLatestVersion()
    {
        if (runningTask != null)
        {
            return;
        }
        State = EState.FetchingVersion;
        runningTask = Task.Run(async () => await FetchLatestVersion());
    }

    public async Task FetchLatestVersion()
    {
        using (HttpClient wc = new HttpClient())
        {
            try
            {
                var data = await wc.GetStringAsync(base_host);
            }
            catch (HttpRequestException e) {
                Godot.GD.Print(e.Message);
                Godot.GD.Print("Failed to fetch update information.");
                return;
            }
            catch (InvalidOperationException e)
            {
                Godot.GD.Print(e.Message);
                Godot.GD.Print("InvalidOperationException .");
                return;
            }
            catch (TaskCanceledException e)
            {
                Godot.GD.Print(e.Message);
                Godot.GD.Print("Canceled.");
                return;
            }
        }

        Godot.GD.Print("Hello world.");
    }

    public async Task FetchPatchData(string from, string to)
    {
        
    }

    public async Task FetchFile(string file)
    {
        
    }

    public async Task<Result<byte[]>> FetchPckFile(string file)
    {
        throw new Exception();
    }



}
