public enum InstantType
{
    Instant,        // not removed from RQ, resolved immediately
    HalfInstant,    // removed from RQ, resolved immediately
    NonInstant,     // removed from RQ, resolved at end of instant by speed order
    PassiveBuff,    // stat modifier only, no resolve animation, recalculated on add/remove
}