namespace IsometricShooter.Core
{
    public interface IItem
    {
        string ItemId { get; }
        string ItemName { get; }
        ItemType ItemType { get; }
        int StackSize { get; }
    }
}