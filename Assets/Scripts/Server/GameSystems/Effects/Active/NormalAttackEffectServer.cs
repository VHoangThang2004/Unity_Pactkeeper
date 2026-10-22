// Server/GameSystems/Effects/Active/NormalAttackEffectServer.cs
using UnityEngine;

// Single-target attack. All logic lives in DamageEffectServerBase (melee and projectile assets differ only in client VFX).
[CreateAssetMenu(fileName = "NormalAttackEffectServer", menuName = "SRPG/Effects/Server/NormalAttack")]
public class NormalAttackEffectServer : DamageEffectServerBase { }
