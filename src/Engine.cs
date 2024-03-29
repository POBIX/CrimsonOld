﻿using System.Diagnostics;
using Crimson;
using GLFW;
using ImGuiNET;
using OpenGL;
using Monitor = GLFW.Monitor;

namespace Crimson;

public static class Engine
{
    // why is there both a handle and a WindowHandle you may ask?
    // well, for some reason,
    // somebody thought it was a good idea to make Glfw.CreateWindow() return a Window instead of an IntPtr.
    // but to make it so that there's no cast between IntPtr and Window. Even though it's just a statically typed pointer.
    // literally the only thing Window has over IntPtr is an Opacity property. Which you can get using a function call.
    // Every other call is exactly the same as in C.
    // So we need to keep track of the Window object in order to call functions for ourselves,
    // but to expose it as an IntPtr for anybody who wants to use different GLFW bindings together with this.
    // thank you person who developed glfw-net very cool!
    internal static Window handle;

    /// <summary>
    /// The GLFW window's memory address.
    /// </summary>
    public static IntPtr WindowHandle => handle;

    /// <summary>
    /// Is the window currently active? False when the user exits the window or when you call <see cref="Quit"/>.
    /// </summary>
    public static bool Running { get; private set; }

    private static int width;
    private static int height;

    // this is a field because we get an invocation of a garbage collected delegate otherwise.
    private static SizeCallback resizeCallback = (_, w, h) =>
    {
        width = w;
        height = h;
        Resize?.Invoke(w, h);
    };

    /// <summary> The window's width. </summary>
    public static int Width
    {
        get => width;
        set
        {
            width = value;
            Glfw.SetWindowSize(handle, Width, Height);
        }
    }

    /// <summary> The window's height. </summary>
    public static int Height
    {
        get => height;
        set
        {
            height = value;
            Glfw.SetWindowSize(handle, Width, Height);
        }
    }

    /// <summary> The window's size. </summary>
    public static Vector2 Size
    {
        get => new(Width, Height);
        set
        {
            // settings the fields and not the properties on purpose
            width = (int)value.x;
            height = (int)value.y;
            Glfw.SetWindowSize(handle, width, height);
        }
    }

    /// <summary>
    /// Gets invoked when the window's size changes. First parameter is the new width, second is the new height.
    /// </summary>
    public static event Action<int, int> Resize;

    private static bool vsync;

    /// <summary> Vertical Synchronization for the window. </summary>
    public static bool VSync
    {
        get => vsync;
        set
        {
            vsync = value;
            Glfw.SwapInterval(VSync ? 1 : 0);
        }
    }

    private static bool fullscreen;
    /// <summary> Takes the window in or out of true fullscreen. </summary>
    public static bool Fullscreen
    {
        get => fullscreen;
        set
        {
            fullscreen = value;
            VideoMode m = Glfw.GetVideoMode(Glfw.PrimaryMonitor);
            if (Fullscreen)
                Glfw.SetWindowMonitor(handle, Glfw.PrimaryMonitor, 0, 0, m.Width, m.Height, m.RefreshRate);
            else
                Glfw.SetWindowMonitor(
                    handle, Monitor.None, m.Width / 2 - Width / 2, m.Height / 2 - Height / 2, Width, Height,
                    m.RefreshRate
                );
        }
    }

    private static Color clearColor;

    /// <summary> The color to clear the screen with. </summary>
    public static Color ClearColor
    {
        get => clearColor;
        set
        {
            clearColor = value;
            Gl.ClearColor(ClearColor.r, ClearColor.g, ClearColor.b, ClearColor.a);
        }
    }

    /// <summary> The constant physics step in seconds - delta in Update(). </summary>
    public static float PhysicsStep { get; set; } = 1 / 60f;

    private static float lastFrame;
    private static float currFrame;
    private static float accumulator = 0;

    /// <summary>
    /// The currently active scene, which we will be updating and drawing.
    /// </summary>
    public static Scene Scene { get; set; }

    /// <summary>
    /// The seconds elapsed since the last frame or the constant physics step -
    /// depends on which method (Frame() or Update()) was called last.
    /// </summary>
    public static float Delta { get; private set; }

    /// <summary>
    /// The amount of seconds that have passed since the last frame - delta in Frame().
    /// </summary>
    public static float FrameTime { get; private set; }

    /// <summary>
    /// The amount of time in seconds since the engine was created.
    /// </summary>
    public static float Elapsed { get; private set; }

    /// <summary>
    /// Is the engine currently drawing?
    /// </summary>
    public static bool Drawing { get; private set; }

    /// <summary>
    /// Has Update() been called this frame?
    /// </summary>
    public static bool Updated { get; private set; }

    /// <inheritdoc cref="Light.Ambience"/>
    public static float AmbientLight
    {
        get => Light.Ambience;
        set => Light.Ambience = value;
    }

    private static string title;
    public static string Title
    {
        get => title;
        set
        {
            title = value;
            Glfw.SetWindowTitle(handle, Title);
        }
    }

    /// <summary>
    /// GUI Controller. Use ImGuiNET.ImGui.* inside of an Update() or Frame() function in order to create UI.
    /// </summary>
    public static ImGuiController GUI { get; private set; }

    /// <summary>
    /// Creates a window and initializes Engine. Can only be called once.
    /// </summary>
    /// <param name="width">The window's width</param>
    /// <param name="height">The window's height</param>
    /// <param name="title">The window's title</param>
    public static void Create(int width, int height, string title, bool fullscreen = false, bool borderless = false)
    {
        // not using the properties on purpose, as they'll resize the window, which doesn't even exist yet.
        Engine.width = width;
        Engine.height = height;
        Engine.title = title;

        Glfw.Init();

        Glfw.WindowHint(Hint.ContextVersionMajor, 4);
        Glfw.WindowHint(Hint.ContextVersionMinor, 6);
        Glfw.WindowHint(Hint.OpenglProfile, Profile.Core);
        Glfw.WindowHint(Hint.Decorated, !borderless);

        handle = Glfw.CreateWindow(
            width, height, "Loading...", fullscreen ? Glfw.PrimaryMonitor : Monitor.None, Window.None
        );
        Glfw.SwapBuffers(handle); // prevent screen from being white during initialization (black instead; eyes happy)

        Gl.Initialize();

        Glfw.MakeContextCurrent(handle);

        Glfw.SetCloseCallback(handle, _ => Quit());
        Glfw.SetWindowSizeCallback(handle, resizeCallback);

        Resize += (w, h) => Gl.Viewport(0, 0, w, h);

        ClearColor = Color.Black;
        Gl.Enable(EnableCap.Blend);
        Gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        Input.Init();
        AudioPlayer.Init();
        Graphics.Init();
        Scene.InitTweens();
        GUI = new();
        Glfw.SetCharCallback(handle, (_, point) => GUI.PressChar((char)point));
        Glfw.SetScrollCallback(handle, (_, x, y) => ImGuiController.MouseScroll(new((float)x, (float)y)));

        currFrame = (float)Glfw.Time;

        Scene = new();

        Running = true;

        Glfw.SetWindowTitle(handle, title);
    }

    public static void Create(string settingsPath)
    {
        INI.Section[] sections = INI.Parse(File.ReadAllText(settingsPath)).ToArray();
        Vector2 winSize = new(1280, 720);
        string winTitle = "My Game";
        bool winFull = false;
        bool winBorderless = false;

        bool sizeChanged = false;
        foreach (var (key, value) in sections.Find("Window")!)
        {
            switch (key)
            {
                case "Size":
                    winSize = (Vector2)value;
                    sizeChanged = true;
                    break;
                case "Title":
                    winTitle = (string)value;
                    break;
                case "Fullscreen":
                    winFull = (bool)value;
                    break;
                case "Borderless":
                    winBorderless = (bool)value;
                    break;
                default:
                    throw new FormatException($"Unexpected key {key} in [Window] section of settings file.");
            }
        }
        // if we set fullscreen without specifying a size, then it should be equal to the monitor's resolution.
        if (winFull && !sizeChanged)
        {
            VideoMode mode = Glfw.GetVideoMode(Glfw.PrimaryMonitor);
            winSize = new(mode.Width, mode.Height);
        }
        Create((int)winSize.x, (int)winSize.y, winTitle, winFull, winBorderless);
        LoadSettings(sections);
    }

    private static float GetFrameTime()
    {
        lastFrame = currFrame;
        currFrame = (float)Glfw.Time;
        return currFrame - lastFrame;
    }

    /// <summary>
    /// Automatically gets called by <see cref="Scene.Load"/>.
    /// Calls Start() for the scene.
    /// Needs to be called once after initializing your scene if you don't use <see cref="Scene.Load"/>.
    /// </summary>
    public static void Start() => Scene.Start();

    public static void SimpleUpdate()
    {
        FrameTime = GetFrameTime();
        accumulator += FrameTime;

        Graphics.Queue = Graphics.fQueue;

        Delta = FrameTime;
        Elapsed += Delta;

        Input.SetFrame();
        Input.Update();
        Scene.Frame(FrameTime);

        Delta = PhysicsStep;

        Graphics.Queue = Graphics.uQueue;
        Input.SetUpdate();
        Updated = false;
        while (accumulator >= PhysicsStep)
        {
            Updated = true;
            Input.Update();
            Scene.Update(PhysicsStep);
            accumulator -= PhysicsStep;
        }

        Graphics.Queue = Graphics.fQueue;
        Input.SetFrame();

        Glfw.PollEvents();
    }

    /// <summary>
    /// Updates and renders the scene. Should get called every frame, and only when <see cref="Running"/> is true.
    /// </summary>
    public static void Update()
    {
        GUI.Update(FrameTime);
        SimpleUpdate();
        BeginDraw();
        GUI.Render();
        EndDraw();
    }

    public static void BeginDraw()
    {
        Drawing = true;
        Graphics.fbo.Bind();
        Graphics.Clear();
    }

    public static void EndDraw()
    {
        Scene.Draw();
        Graphics.Draw();
        Graphics.RenderToScreen();
        Drawing = false;
        Glfw.SwapBuffers(handle);
    }

    public static void LoadSettingsSource(string source) =>
        LoadSettings(INI.Parse(source).ToArray());

    public static void LoadSettings(string filePath) =>
        LoadSettingsSource(File.ReadAllText(filePath));

    private static void LoadSettings(INI.Section[] sections)
    {
        INI.ParseSettings(sections.Find("Settings")!.Value);
        INI.Section? input = sections.Find("Input");
        if (input != null) INI.ParseInput(input.Value);
        else INI.ParseInput(sections.FindSubs("Input"));
    }

    public static void LoadScene(SceneGenerator scene)=> Scene.Load(scene);
    public static void LoadScene<T>() where T : SceneGenerator, new() => Scene.Load<T>();

    /// <summary>
    /// Sets <see cref="Running"/> to false, closes the window, and frees resources.
    /// </summary>
    public static void Quit()
    {
        Running = false;
        AudioPlayer.Terminate();
    }
}
