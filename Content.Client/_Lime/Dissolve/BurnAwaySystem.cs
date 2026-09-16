using Content.Shared._Lime.Dissolve;
using Content.Shared.Body;
using Robust.Client.GameObjects;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Client._Lime.Dissolve;

/// <summary>
/// Plays the burn-away animation on a local copy of a deleted body.
/// A bounded hidden cache allows the network event to arrive after entity deletion.
/// </summary>
public sealed class BurnAwaySystem : EntitySystem
{
    [Dependency] private readonly DissolveSystem _dissolve = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float Duration = 3f;
    private const float CacheSeconds = 2f;
    private const int MaxPending = 128;
    private readonly Dictionary<NetEntity, PendingBurn> _pending = new();
    private readonly List<NetEntity> _finished = new();
    private bool _flushing;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<BurnAwayEvent>(OnBurn);
        SubscribeLocalEvent<BodyComponent, MoveEvent>(OnMove);
        EntityManager.BeforeEntityFlush += OnBeforeFlush;
        EntityManager.AfterEntityFlush += OnAfterFlush;
    }

    public override void Shutdown()
    {
        EntityManager.BeforeEntityFlush -= OnBeforeFlush;
        EntityManager.AfterEntityFlush -= OnAfterFlush;
        foreach (var pending in _pending.Values)
        {
            if (pending.Clone is { } clone && !TerminatingOrDeleted(clone))
                QueueDel(clone);
        }
        _pending.Clear();
        _finished.Clear();
        base.Shutdown();
    }

    private void OnBeforeFlush()
    {
        _flushing = true;
        _pending.Clear();
        _finished.Clear();
    }

    private void OnAfterFlush()
    {
        _flushing = false;
    }

    private void OnBurn(BurnAwayEvent args)
    {
        if (_flushing)
            return;

        if (_pending.TryGetValue(args.Entity, out var pending))
            pending.Confirmed = true;
        else if (_pending.Count < MaxPending)
            _pending.Add(args.Entity, new PendingBurn(_timing.RealTime + TimeSpan.FromSeconds(CacheSeconds)) { Confirmed = true });
    }

    private void OnMove(Entity<BodyComponent> ent, ref MoveEvent args)
    {
        if (!args.ParentChanged || _flushing || IsClientSide(ent))
            return;

        var netEntity = GetNetEntity(ent);
        if (args.NewPosition.EntityId.IsValid())
        {
            // Возврат из PVS делает старый снимок неактуальным.
            if (!args.OldPosition.EntityId.IsValid() && _pending.Remove(netEntity, out var stale) &&
                stale.Clone is { } staleClone && !TerminatingOrDeleted(staleClone))
                QueueDel(staleClone);
            return;
        }

        if (!TryComp<SpriteComponent>(ent, out var source) ||
            !source.Visible || source.ContainerOccluded)
            return;

        // ProcessDeletions сначала вызывает DetachEntity, и только затем EntityTerminatingEvent.
        // Здесь ещё целы слои органов, а прежние координаты и поворот доступны в MoveEvent.
        var coordinates = args.OldPosition;
        if (TerminatingOrDeleted(coordinates.EntityId) ||
            !TryComp<TransformComponent>(coordinates.EntityId, out var parent) ||
            parent.MapUid is not { } map || TerminatingOrDeleted(map))
            return;

        if (!_pending.TryGetValue(netEntity, out var pending))
        {
            if (_pending.Count >= MaxPending)
                return;
            pending = new PendingBurn(_timing.RealTime + TimeSpan.FromSeconds(CacheSeconds));
            _pending.Add(netEntity, pending);
        }

        if (pending.Clone != null)
            return;

        var clone = Spawn(null, coordinates);
        var sprite = AddComp<SpriteComponent>(clone);
        _sprite.CopySprite((ent.Owner, source), (clone, sprite));
        // CopySprite дублирует изменяемый PostShader. Копии нужен собственный dissolve,
        // а не чужой outline/stealth, ссылающийся на уже удаляемую сущность.
        if (sprite.PostShader != source.PostShader)
            sprite.PostShader?.Dispose();
        sprite.PostShader = null;
        _sprite.SetSnapCardinals((clone, sprite), source.SnapCardinals);
        _sprite.SetVisible((clone, sprite), false);
        _transform.SetLocalRotationNoLerp(clone, args.OldRotation);
        AddComp<TimedDespawnComponent>(clone).Lifetime = CacheSeconds + Duration;
        pending.Clone = clone;
        Log.Debug($"Cached burn-away sprite for {ToPrettyString(ent)} on clone {clone}.");
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        _finished.Clear();
        foreach (var (netEntity, pending) in _pending)
        {
            if (pending.Clone is { } clone && !TerminatingOrDeleted(clone) && pending.Confirmed)
            {
                _sprite.SetVisible(clone, true);
                if (_dissolve.StartDissolve(clone, Duration, DissolveSettings.Burn))
                {
                    Comp<TimedDespawnComponent>(clone).Lifetime = Duration + 0.1f;
                    Log.Debug($"Started {Duration}s burn-away for deleted net entity {netEntity} on clone {clone}.");
                }
                else
                    QueueDel(clone);
                _finished.Add(netEntity);
            }
            else if (_timing.RealTime >= pending.Expires)
            {
                if (pending.Clone is { } expired && !TerminatingOrDeleted(expired))
                    QueueDel(expired);
                _finished.Add(netEntity);
            }
        }

        foreach (var netEntity in _finished)
            _pending.Remove(netEntity);
    }

    private sealed class PendingBurn(TimeSpan expires)
    {
        public readonly TimeSpan Expires = expires;
        public EntityUid? Clone;
        public bool Confirmed;
    }
}
