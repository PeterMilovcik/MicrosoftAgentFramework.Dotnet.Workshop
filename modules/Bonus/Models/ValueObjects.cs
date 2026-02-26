using System.Text.Json;
using System.Text.Json.Serialization;

namespace RPGGameMaster.Models;

/// <summary>
/// Value object for hit points: current HP + max HP with clamped arithmetic.
/// Invariant: 0 ≤ HP ≤ MaxHP, MaxHP > 0.
/// </summary>
[JsonConverter(typeof(HitPointsConverter))]
internal readonly record struct HitPoints
{
    public int Current { get; }
    public int Max { get; }

    public HitPoints(int current, int max)
    {
        Max = Math.Max(1, max);
        Current = Math.Clamp(current, 0, Max);
    }

    public bool IsAlive => Current > 0;
    public bool IsDead => Current <= 0;
    public int Percentage => (int)(100.0 * Current / Max);

    /// <summary>Take damage, clamped to 0.</summary>
    public HitPoints TakeDamage(int amount) => new(Current - Math.Max(0, amount), Max);

    /// <summary>Heal, clamped to MaxHP.</summary>
    public HitPoints Heal(int amount) => new(Current + Math.Max(0, amount), Max);

    /// <summary>Heal and report actual amount healed (clamped to missing HP).</summary>
    public HitPoints Heal(int amount, out int actualHealed)
    {
        var after = new HitPoints(Current + Math.Max(0, amount), Max);
        actualHealed = after.Current - Current;
        return after;
    }

    /// <summary>Restore to full HP.</summary>
    public HitPoints RestoreToMax() => new(Max, Max);

    /// <summary>Increase max HP (and optionally restore to new max).</summary>
    public HitPoints IncreaseMax(int amount, bool restoreToMax = false)
    {
        var newMax = Max + amount;
        return restoreToMax ? new(newMax, newMax) : new(Current, newMax);
    }

    public override string ToString() => $"{Current}/{Max}";

    // Deconstruct for convenient access
    public void Deconstruct(out int current, out int max)
    {
        current = Current;
        max = Max;
    }
}

/// <summary>
/// Serializes HitPoints as two flat JSON properties "hp" and "max_hp" on the parent object.
/// When used as a standalone property, serializes as {"hp": N, "max_hp": N}.
/// </summary>
internal sealed class HitPointsConverter : JsonConverter<HitPoints>
{
    public override HitPoints Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        int hp = 0, maxHp = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var prop = reader.GetString();
                reader.Read();
                if (prop is "hp") hp = reader.GetInt32();
                else if (prop is "max_hp") maxHp = reader.GetInt32();
            }
        }
        return new HitPoints(hp, maxHp);
    }

    public override void Write(Utf8JsonWriter writer, HitPoints value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("hp", value.Current);
        writer.WriteNumber("max_hp", value.Max);
        writer.WriteEndObject();
    }
}

/// <summary>
/// Value object for gold: non-negative integer with arithmetic.
/// </summary>
[JsonConverter(typeof(GoldConverter))]
internal readonly record struct Gold : IComparable<Gold>
{
    public int Amount { get; }

    public Gold(int amount) => Amount = Math.Max(0, amount);

    public static Gold Zero => new(0);

    public Gold Add(int amount) => new(Amount + amount);
    public Gold Subtract(int amount) => new(Amount - amount);
    public bool CanAfford(int price) => Amount >= price;

    /// <summary>Spend gold — fails if not enough (returns false).</summary>
    public bool TrySpend(int price, out Gold remaining)
    {
        if (Amount < price) { remaining = this; return false; }
        remaining = new(Amount - price);
        return true;
    }

    /// <summary>Apply a fractional penalty (e.g. 0.5 for death), returns amount lost.</summary>
    public Gold ApplyPenalty(double fraction, out int amountLost)
    {
        amountLost = (int)(Amount * fraction);
        return new(Amount - amountLost);
    }

    public int CompareTo(Gold other) => Amount.CompareTo(other.Amount);

    // Implicit conversions for easy arithmetic interop
    public static implicit operator int(Gold g) => g.Amount;
    public static implicit operator Gold(int amount) => new(amount);

    // Arithmetic operators so += / -= work on the property
    public static Gold operator +(Gold g, int amount) => g.Add(amount);
    public static Gold operator -(Gold g, int amount) => g.Subtract(amount);

    public override string ToString() => Amount.ToString();
}

internal sealed class GoldConverter : JsonConverter<Gold>
{
    public override Gold Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetInt32());

    public override void Write(Utf8JsonWriter writer, Gold value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.Amount);
}

/// <summary>
/// Value object for experience points: non-negative integer with arithmetic.
/// Follows the same pattern as <see cref="Gold"/>.
/// </summary>
[JsonConverter(typeof(ExperienceConverter))]
internal readonly record struct Experience
{
    public int Amount { get; }

    public Experience(int amount) => Amount = Math.Max(0, amount);

    public Experience Add(int amount) => new(Amount + amount);

    /// <summary>Apply a fractional penalty (e.g. 0.25 for death), returns amount lost.</summary>
    public Experience ApplyPenalty(double fraction, out int amountLost)
    {
        amountLost = (int)(Amount * fraction);
        return new(Amount - amountLost);
    }

    public static implicit operator int(Experience e) => e.Amount;
    public static implicit operator Experience(int amount) => new(amount);

    public static Experience operator +(Experience e, int amount) => e.Add(amount);
    public static Experience operator -(Experience e, int amount) => new(e.Amount - amount);

    public override string ToString() => Amount.ToString();
}

internal sealed class ExperienceConverter : JsonConverter<Experience>
{
    public override Experience Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetInt32());

    public override void Write(Utf8JsonWriter writer, Experience value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.Amount);
}

/// <summary>
/// Value object for NPC disposition toward the player: bounded integer [-100, +100]
/// with named tiers: hostile, unfriendly, neutral, friendly, warm, devoted.
/// </summary>
[JsonConverter(typeof(DispositionConverter))]
internal readonly record struct Disposition : IComparable<Disposition>
{
    public const int MinValue = -100;
    public const int MaxValue = 100;

    public int Value { get; }

    public Disposition(int value) => Value = Math.Clamp(value, MinValue, MaxValue);

    public static Disposition Neutral => new(0);

    /// <summary>Improve disposition (positive delta), clamped.</summary>
    public Disposition Improve(int amount) => new(Value + Math.Abs(amount));

    /// <summary>Worsen disposition (negative delta), clamped.</summary>
    public Disposition Worsen(int amount) => new(Value - Math.Abs(amount));

    /// <summary>Named tier for prompts and display.</summary>
    public string Label => Value switch
    {
        <= -50 => "hostile",
        <= -20 => "unfriendly",
        <= 10  => "neutral",
        <= 40  => "friendly",
        <= 70  => "warm",
        _      => "devoted",
    };

    public int CompareTo(Disposition other) => Value.CompareTo(other.Value);

    public static implicit operator int(Disposition d) => d.Value;

    public override string ToString() => $"{Value} ({Label})";
}

internal sealed class DispositionConverter : JsonConverter<Disposition>
{
    public override Disposition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetInt32());

    public override void Write(Utf8JsonWriter writer, Disposition value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.Value);
}

/// <summary>
/// Value object for entity identifiers: 8-character lowercase hex string.
/// </summary>
[JsonConverter(typeof(EntityIdConverter))]
internal readonly record struct EntityId
{
    public string Value { get; }

    public EntityId(string value) => Value = value ?? "";

    /// <summary>Generate a new unique entity ID.</summary>
    public static EntityId New() => new(Guid.NewGuid().ToString("N")[..8]);

    public static EntityId Empty => new("");

    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    // Implicit conversions for seamless use as dictionary key / string comparisons
    public static implicit operator string(EntityId id) => id.Value;
    public static implicit operator EntityId(string s) => new(s);

    public override string ToString() => Value;
}

internal sealed class EntityIdConverter : JsonConverter<EntityId>
{
    public override EntityId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetString() ?? "");

    public override void Write(Utf8JsonWriter writer, EntityId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
