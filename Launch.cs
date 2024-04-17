using Godot;
using PatchLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using static Godot.WebSocketPeer;

public partial class Launch : Godot.Control
{
    [Godot.Export]
    public Godot.PackedScene GameScene;

    // SETTINGS
    [Godot.Export]
    public string BaseHost = "http://localhost:10000";

    [Godot.Export]
    public Godot.Button playButton;

    [Godot.Export]
    public Godot.Label progressLabel;

    [Godot.Export]
    public Godot.ProgressBar progressBar;

    [Godot.Export]
    public Godot.Label overallProgressLabel;

    [Godot.Export]
    public Godot.ProgressBar progressBarOverall;

    [Godot.Export]
    public Godot.Label versionLabel;

    IEnumerator<bool> updating = null;
    Updater updater;
    public override void _Ready()
    {
        base._Ready();
        playButton.Pressed += PlayButton_Pressed;


        var mainDir = new DirectoryInfo(Directory.GetCurrentDirectory());
        updater = new Updater(mainDir, BaseHost);

        updater.PatchProgressOnChanged += (sender) =>
        {
            this.CallDeferred("UpdateProgress");
        };
        updater.PatchOverallProgressOnChanged += (sender) =>
        {
            this.CallDeferred("UpdateProgress");
        };

        updater.CurrentVersionOnChanged += (sender) =>
        {
            this.CallDeferred("UpdateVersionLabel");
        };

        updater.LatestVersionOnChanged += (sender) =>
        {
            this.CallDeferred("UpdateVersionLabel");
        };

        updater.StateOnChanged += (sender) =>
        {
            this.CallDeferred("UpdateVersionLabel");
            this.CallDeferred("UpdatePlayButton");
        };

        updater.CurrentPatchMessageOnChanged += (sender) =>
        {
            this.CallDeferred("UpdatePatchLabel");
        };


        //var dir = Directory.GetCurrentDirectory();
        /*
        var tt = new DirectoryInfo(Path.Combine(dir, "test11"));
        foreach (var (curr, max, inst) in PatchLibrary.Patch.ApplyPatch(output, tt)) {
            GD.Print($" {curr} / {max} {inst}");
        }*/


        Dictionary<string, string> arguments = new Dictionary<string, string>();
        foreach(var argument in OS.GetCmdlineUserArgs())
        {
            if (argument.Contains("="))
            {
                var splitted = argument.Split("=");
                arguments.Add(splitted[0].TrimStart('-'), splitted[1]);
            }
            else
            {
                arguments.Add(argument.TrimStart('-'), null);
            }
        }

        if(arguments.ContainsKey("createpatch"))
        {
            // TODO parse user input better...

            var output = new FileInfo(arguments["output"]);
            var from = new DirectoryInfo(arguments["from"]);
            var to = new DirectoryInfo(arguments["to"]);
            PatchLibrary.Patch.CreatePatch(output, from, to);
        }
        else
        {
            updating = updater.RunUpdateEnumerable();
        }
    }

    void UpdateVersionLabel()
    {
        if (updater.State == EState.Error)
        {
            versionLabel.Text = $"Error";
        }
        else if (updater.State == EState.UpToDate || updater.State == EState.None)
        {
            versionLabel.Text = $"{updater.CurrentVersion}";
        }
        else
        {
            versionLabel.Text = $"{updater.CurrentVersion} -> {updater.LatestVersion}";
        }
    }

    void UpdateProgress()
    {
        progressBar.MinValue = 0;
        progressBar.MaxValue = 100;
        progressBar.Value = updater.PatchProgress;
        progressBarOverall.MinValue = 0;
        progressBarOverall.MaxValue = 100;
        progressBarOverall.Value = updater.PatchOverallProgress;
    }

    void UpdatePlayButton()
    {
        playButton.Disabled = updater.State != EState.UpToDate;
    }

    void UpdatePatchLabel()
    {
        this.progressLabel.Text = updater.CurrentPatchMessage;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if(updating != null)
        {
            GD.Print("Test");
            if(!updating.MoveNext())
            {
                updating = null;
                GD.Print("done");
                if(updater.DidPatch)
                {
                    System.Diagnostics.Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location); // to start new instance of application
                    GetTree().Quit();
                }
            }
        }
    }

    private void PlayButton_Pressed()
    {
        var nextScene = GameScene.Instantiate();
        var oldScene = GetTree().Root.GetChild(0);
        GetTree().Root.AddChild(nextScene);
        oldScene.QueueFree();

        // Dont forget to apply settings in new scene
    }
}
