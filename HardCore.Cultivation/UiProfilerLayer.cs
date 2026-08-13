#if !ANDROID
using System.Numerics;
using Autofac;
using ImGuiNET;
using JetBrains.Annotations;
using Vecxy.Editor;
using Vecxy.Engine;
using Vecxy.Rendering;
using Vecxy.UI;

namespace HardCore.Cultivation;

/// <summary>
/// Lightweight in-game UI profiler. It deliberately lives outside the editor and
/// renders after the game/UI pass, so profiling does not alter the XML/CSS tree.
/// </summary>
[UsedImplicitly]
public sealed class UiProfilerLayer(
    IRenderOverlayStage overlays,
    IRenderer renderer,
    IUiDiagnostics uiDiagnostics,
    ImGuiRenderer imgui) : AAppLayer
{
    public sealed class Definition : ADefinition<UiProfilerLayer>
    {
        public override void RegisterLocal(ContainerBuilder builder)
        {
            builder.RegisterType<ImGuiRenderer>().AsSelf().SingleInstance();
        }
    }

    private bool _initialized;

    public override void OnInitialize()
    {
        if (_initialized)
            return;
        imgui.Initialize();
        imgui.DisableIniPersistence();
        overlays.RegisterOverlay(DrawOverlay);
        _initialized = true;
    }

    public override void OnUpdate(float deltaTime)
    {
        if (_initialized)
            imgui.BeginFrame(deltaTime);
    }

    public override void OnUnload()
    {
        if (!_initialized)
            return;
        overlays.UnregisterOverlay(DrawOverlay);
        _initialized = false;
    }

    private void DrawOverlay()
    {
        var rendering = renderer.Statistics;
        var ui = uiDiagnostics.Statistics;
        var viewport = ImGui.GetMainViewport();
        ImGui.SetNextWindowPos(viewport.WorkPos + new Vector2(10.0f), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.88f);

        const ImGuiWindowFlags flags =
            ImGuiWindowFlags.NoDecoration |
            ImGuiWindowFlags.AlwaysAutoResize |
            ImGuiWindowFlags.NoSavedSettings |
            ImGuiWindowFlags.NoFocusOnAppearing |
            ImGuiWindowFlags.NoNav |
            ImGuiWindowFlags.NoInputs;

        if (ImGui.Begin("UI PROFILER##in_game_ui_profiler", flags))
        {
            ImGui.TextColored(new Vector4(0.45f, 0.95f, 0.72f, 1.0f), "VECXY UI PROFILER");
            ImGui.SameLine();
            ImGui.TextDisabled($"frame {ui.Frame}");
            ImGui.Separator();

            Metric("Frame", rendering.FrameTimeMilliseconds, "ms", 12.0, 16.7);
            ImGui.SameLine(190.0f);
            ImGui.Text($"{rendering.FramesPerSecond,5:F1} FPS");
            ImGui.Text($"Scene draw calls  {rendering.DrawCalls,7:N0}");

            ImGui.Separator();
            ImGui.Text("UI CPU                    now      avg     peak");
            Timing("Update", ui.UpdateCpu);
            Timing("  UI pipeline", ui.LayoutCpu);
            Timing("    CSS resolve", ui.StyleCpu);
            Timing("    Yoga apply", ui.LayoutApplyCpu);
            Timing("    Yoga solve", ui.YogaCpu);
            Timing("      Text measure", ui.TextMeasureCpu);
            Timing("    Arrange/read", ui.ArrangeCpu);
            Timing("    Grid", ui.GridCpu);
            Timing("    Scroll extent", ui.ScrollExtentCpu);
            Timing("  Animation", ui.AnimationCpu);
            Timing("  Hit test", ui.HitTestCpu);
            Timing("Render", ui.RenderCpu);
            Timing("  Render build", ui.TessellationCpu);
            Timing("  GPU upload", ui.UploadCpu);
            Timing("  Draw submit", ui.LayerDrawCpu);

            ImGui.Separator();
            ImGui.Text($"DOM       {ui.Elements,7:N0} total  {ui.VisibleElements,7:N0} visible  {ui.InteractiveElements,5:N0} hit");
            ImGui.Text($"Geometry  {ui.Vertices,7:N0} verts  {ui.Indices,7:N0} indices  {ui.Batches,5:N0} batches");
            ImGui.Text($"Cache     {ui.LayerRebuilds,7:N0} rebuild  {ui.LayerCacheHits,7:N0} hit  {ui.ActiveAnimations,5:N0} anim");
            ImGui.Text($"Upload    {FormatBytes(ui.UploadBytes),7}   shadows {ui.ShadowDefinitions,4:N0}/{ui.ShadowLayers,4:N0}");
            ImGui.Text($"GC update {FormatBytes(ui.UpdateAllocatedBytes),7}   render {FormatBytes(ui.RenderAllocatedBytes),7}");
            ImGui.Text($"  layout {FormatBytes(ui.LayoutAllocatedBytes),7}  anim {FormatBytes(ui.AnimationAllocatedBytes),7}  input {FormatBytes(ui.InputAllocatedBytes),7}");
            ImGui.Text($"Work      style {ui.StyledElements,4}  layout {ui.LayoutNodes,4}  arrange {ui.ArrangedNodes,4}  text {ui.TextMeasureCount,3}");
            ImGui.Text($"Full layouts this frame: {ui.FullLayoutCount}");
            ImGui.Text($"Last work CSS/Yoga/Build: {ui.StyleCpu.LastWorkMilliseconds:F2} / {ui.YogaCpu.LastWorkMilliseconds:F2} / {ui.TessellationCpu.LastWorkMilliseconds:F2} ms");

            foreach (var document in ui.Documents)
            {
                var state = !document.Visible
                    ? "hidden"
                    : document.RebuiltThisFrame ? "REBUILD" : "cached";
                var color = document.RebuiltThisFrame
                    ? new Vector4(1.0f, 0.65f, 0.30f, 1.0f)
                    : new Vector4(0.65f, 0.75f, 0.70f, 1.0f);
                ImGui.Separator();
                ImGui.TextColored(color, $"{ShortPath(document.Path)}  [{state}]");
                ImGui.Text($"  nodes {document.Elements,4}  batches {document.Batches,4}  upload {FormatBytes(document.UploadBytes),7}");
                ImGui.Text($"  dirty S/L/P {document.StyleChangesThisFrame,4}/{document.LayoutChangesThisFrame,4}/{document.VisualChangesThisFrame,4}  passes {document.StylePasses}/{document.LayoutPasses}");
            }
        }
        ImGui.End();
        imgui.Render();
    }

    private static void Timing(string name, UiTimingStatistics timing)
    {
        var color = timing.CurrentMilliseconds >= 4.0
            ? new Vector4(1.0f, 0.40f, 0.32f, 1.0f)
            : timing.CurrentMilliseconds >= 1.0
                ? new Vector4(1.0f, 0.76f, 0.32f, 1.0f)
                : new Vector4(0.78f, 0.86f, 0.82f, 1.0f);
        ImGui.TextColored(color,
            $"{name,-18} {timing.CurrentMilliseconds,7:F3} {timing.AverageMilliseconds,7:F3} {timing.PeakMilliseconds,7:F3}");
    }

    private static void Metric(string name, double value, string unit, double warning, double critical)
    {
        var color = value >= critical
            ? new Vector4(1.0f, 0.35f, 0.28f, 1.0f)
            : value >= warning
                ? new Vector4(1.0f, 0.75f, 0.30f, 1.0f)
                : new Vector4(0.50f, 0.95f, 0.70f, 1.0f);
        ImGui.TextColored(color, $"{name,-10} {value,7:F2} {unit}");
    }

    private static string ShortPath(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash >= 0 ? path[(slash + 1)..] : path;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024L)
            return $"{bytes / (1024.0 * 1024.0):F1}M";
        if (bytes >= 1024L)
            return $"{bytes / 1024.0:F1}K";
        return $"{bytes}B";
    }
}
#endif
