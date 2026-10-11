// Bosses that the player's Siren's Chain can stun and slow (instead of pulling and holding them).
// stunTime: they stop what they are doing for this long. Then for slowTime they are slower
// (slowMultiplier 0.5 = half speed).
public interface IChainStunnable
{
    void ChainStun(float stunTime, float slowMultiplier, float slowTime);
}
