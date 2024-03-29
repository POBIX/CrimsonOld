﻿namespace Crimson;

public struct CollisionLayer
{
    public ulong Data { get; set; }

    public static CollisionLayer All => new(ulong.MaxValue);
    public static CollisionLayer None => new(0);

    public CollisionLayer(ulong l) => Data = l;

    public void SetBit(int n, bool val) =>
        Data = val ? Data | (uint)(1 << n) : Data & (uint)~(1 << n);

    public bool GetBit(int n) => (Data & (uint)(1 << n)) == 1;

    public static implicit operator ulong(CollisionLayer c) => c.Data;
    public static implicit operator CollisionLayer(ulong l) => new(l);

    public static CollisionLayer WithBit(int n)
    {
        CollisionLayer l = new(0);
        l.SetBit(n, true);
        return l;
    }
}

/// <summary> Used internally. You're probably looking for the generic version. </summary>
public interface ICollide
{
    bool Block { get; set; }
    CollisionLayer Layer { get; set; }
    internal bool IsStillCollidingAny(ICollide a, ICollide b, Vector2 velocity);
    internal bool IsCollidingAny(ICollide a, ICollide b, Vector2 velocity, out object info);
    internal void RespondAny(Controller body, Vector2 velocity, List<object> collisions);
}

public interface ICollide<in T1, in T2, TInfo> : ICollide where TInfo : new()
{
    bool ICollide.IsCollidingAny(ICollide a, ICollide b, Vector2 velocity, out object info)
    {
        if (a is T1 t1 && b is T2 t2 && IsColliding(t1, t2, velocity, out TInfo tInfo))
        {
            info = tInfo;
            return true;
        }
        info = new TInfo();
        return false;
    }

    void ICollide.RespondAny(Controller body, Vector2 velocity, List<object> collisions) =>
        Respond(body, velocity, collisions.Cast<TInfo>().ToList());

    bool ICollide.IsStillCollidingAny(ICollide a, ICollide b, Vector2 velocity) =>
        IsStillColliding((T1)a, (T2)b, velocity);

    bool IsColliding(T1 a, T2 b, Vector2 velocity, out TInfo info);
    void Respond(Controller body, Vector2 velocity, List<TInfo> collisions);

    /// <summary>
    /// Implement only if your algorithm assumes objects are NOT in collision to begin with.
    /// This will only ever get called if objects were found to be colliding in the previous frame (using <seealso cref="IsColliding"/>),
    /// and is only used with triggers.
    /// </summary>
    bool IsStillColliding(T1 a, T2 b, Vector2 velocity) => IsColliding(a, b, velocity, out _);
}
