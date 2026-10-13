using Microsoft.Extensions.Logging;

namespace HanClassicBuyWheel;

public sealed partial class HanClassicBuyWheelPlugin
{
    private bool _waitingForWorld, _worldReady;

    private bool WorldAvailable()
    {
        try { return Core.EntitySystem.GetGameRules() is not null; }
        // 地图加载期间实体系统尚未建立，这是就绪检查的正常等待状态。
        catch (InvalidOperationException) { return false; }
    }

    private void AwaitWorld()
    {
        _worldReady = false;
        _waitingForWorld = true;
        StopRefresh();
    }

    private void CancelWorld()
    {
        _waitingForWorld = false;
        _worldReady = false;
    }

    private void OnWorldUpdate()
    {
        // 世界更新在空服休眠时也会执行；只恢复规则与刷新，不在加载阶段创建 HUD。
        if (_unloaded || !_waitingForWorld || !WorldAvailable()) return;
        PrepareWorld();
    }

    private void PrepareWorld()
    {
        // 入服事件也可先完成准备，避免依赖与首次世界更新的先后顺序。
        if (_worldReady) return;
        _waitingForWorld = false;
        _worldReady = true;
        try
        {
            if (_catalog is { } catalog) _nativeBuyRules.Apply(catalog.Config);
            StartRefresh();
        }
        catch (Exception ex)
        {
            // 真正的初始化故障只记录一次，不在每次世界更新中刷屏。
            Core.Logger.LogError(ex, "购买轮盘地图初始化失败。");
        }
    }
}
