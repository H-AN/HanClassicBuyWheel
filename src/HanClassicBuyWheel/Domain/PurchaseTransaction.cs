namespace HanClassicBuyWheel.Domain;

public enum PurchaseFailure { None, NotReady, Dead, Team, BuyZone, BuyTime, Disabled, Money, AlreadyOwned, CarryLimit, GiveFailed }
public readonly record struct PurchaseQuote(PurchaseFailure Failure, int Price)
{
    public bool Allowed => Failure == PurchaseFailure.None;
}

public static class PurchaseTransaction
{
    // 同一游戏线程内扣款、发放、退款，不允许跨 await。发放失败只退本次扣款。
    public static bool Execute(int price, Func<int> balance, Action<int> writeBalance, Func<bool> grant)
    {
        if (price < 0 || balance() < price) return false;
        writeBalance(balance() - price);
        bool success = false;
        try { success = grant(); return success; }
        finally { if (!success) writeBalance(checked(balance() + price)); }
    }
}
