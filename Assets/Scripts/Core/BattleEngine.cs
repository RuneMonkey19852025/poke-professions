using System;
using System.Collections.Generic;
using UnityEngine;
using PokeProfessions.UI;

namespace PokeProfessions.Core
{
    public enum BattleResult { Ongoing, PlayerWon, PlayerLost, Caught, Fled }

    /// <summary>
    /// Pure logic battle engine (no GameObjects). The BattleUI drives it
    /// turn by turn and applies rewards to GameState here.
    /// </summary>
    public class BattleEngine
    {
        public List<Creature> PlayerTeam;
        public List<Creature> EnemyTeam;
        public bool IsTrainerBattle;
        public TrainerDef Trainer;
        public int ActivePlayerIdx;
        public int ActiveEnemyIdx;
        public BattleResult Result = BattleResult.Ongoing;

        /// <summary>Mastery technique usable right now by this creature? (player side only)</summary>
        public static bool IsTechniqueAvailable(GameState save, Creature c, string moveId)
        {
            if (save == null || c == null || c.curHP <= 0) return false;
            if (!PlayerTeamStatic.Contains(c)) return false;
            var wc = WeaponMastery.ClassOfTechnique(moveId);
            if (wc == WeaponClass.None) return false;
            // the technique's class must be the weapon or armor actually worn
            if (WeaponMastery.ClassOf(c.weapon) != wc && WeaponMastery.ClassOf(c.armor) != wc) return false;
            if (WeaponMastery.Level(save, wc) < UnlockLevel(moveId)) return false;
            return WeaponMastery.TechPP(save, wc, moveId) > 0;
        }

        static int UnlockLevel(string moveId)
        {
            foreach (var t in WeaponMastery.Techniques)
                if (t.moveId == moveId) return t.unlockLevel;
            return int.MaxValue;
        }

        static readonly List<Creature> PlayerTeamStatic = new List<Creature>();

        public event Action<string> Log;
        public event Action<string> SfxRequest;
        public event Action<Creature> Evolved;
        public event Action<string> CaughtSpecies;

        readonly Dictionary<string, int> atkBuffs = new Dictionary<string, int>();
        readonly Dictionary<string, int> defBuffs = new Dictionary<string, int>();
        bool catchSucceeded;

        public BattleEngine(List<Creature> playerTeam, List<Creature> enemyTeam, TrainerDef trainer = null)
        {
            PlayerTeam = playerTeam ?? new List<Creature>();
            EnemyTeam = enemyTeam ?? new List<Creature>();
            IsTrainerBattle = trainer != null;
            Trainer = trainer;
            InitPP(PlayerTeam);
            InitPP(EnemyTeam);

            // techniques read team membership; keep a static snapshot in sync
            PlayerTeamStatic.Clear();
            foreach (var c in PlayerTeam) if (c != null) PlayerTeamStatic.Add(c);
        }

        GameState saveState; // for weapon mastery lookups (set by BattlePanel/Game)

        /// <summary>Call once before the battle so mastery bonuses and KO XP can apply.</summary>
        public BattleEngine WithState(GameState state)
        {
            saveState = state;
            return this;
        }

        /// <summary>Ensure every creature has a PP entry per move (wild spawns had none — they could only flail).</summary>
        static void InitPP(List<Creature> team)
        {
            if (team == null) return;
            foreach (var c in team)
            {
                if (c == null || c.moves == null) continue;
                foreach (var m in c.moves)
                    if (!c.movePP.ContainsKey(m))
                    {
                        var md = MoveDB.Get(m);
                        c.movePP[m] = md != null ? md.pp : 10;
                    }
            }
        }

        public Creature PlayerMon => Alive(PlayerTeam, ref ActivePlayerIdx);
        public Creature EnemyMon => Alive(EnemyTeam, ref ActiveEnemyIdx);

        static Creature Alive(List<Creature> team, ref int idx)
        {
            if (team == null || team.Count == 0) return null;
            if (idx < 0 || idx >= team.Count) idx = 0;
            if (team[idx] == null || team[idx].curHP <= 0)
            {
                for (int i = 0; i < team.Count; i++)
                    if (team[i] != null && team[i].curHP > 0) { idx = i; break; }
            }
            var c = team[idx];
            return (c != null && c.curHP > 0) ? c : null;
        }

        public void ResetBattleBuffs()
        {
            atkBuffs.Clear();
            defBuffs.Clear();
        }

        // ---------------- player actions ----------------

        /// <summary>Full turn: player move + enemy response + residual.</summary>
        public void PlayerTurn(string moveId)
        {
            if (Result != BattleResult.Ongoing) return;
            var pm = PlayerMon;
            var em = EnemyMon;
            if (pm == null || em == null) return;

            var playerMove = MoveDB.Get(moveId);
            if (playerMove == null) playerMove = MoveDB.Get("tackle");

            // technique: spend its class's PP instead of the creature's move PP
            bool isTech = WeaponMastery.ClassOfTechnique(moveId) != WeaponClass.None;
            if (isTech && saveState != null)
                WeaponMastery.SpendTechPP(saveState, moveId);
            var enemyMove = MoveDB.Get(EnemyChooseMove(em));
            if (enemyMove == null) enemyMove = MoveDB.Get("tackle");

            bool pPrio = playerMove.id == "flashstep";
            bool ePrio = enemyMove.id == "flashstep";
            bool playerFirst = (pPrio != ePrio) ? pPrio : pm.SPD >= em.SPD;

            if (playerFirst)
            {
                Execute(pm, em, playerMove, true);
                var nextEnemy = EnemyMon;
                if (Result == BattleResult.Ongoing && nextEnemy != null && nextEnemy.curHP > 0)
                    Execute(nextEnemy, PlayerMon, enemyMove, false);
            }
            else
            {
                Execute(em, pm, enemyMove, false);
                var nextPlayer = PlayerMon;
                if (Result == BattleResult.Ongoing && nextPlayer != null && nextPlayer.curHP > 0)
                    Execute(nextPlayer, EnemyMon, playerMove, true);
            }

            EndOfTurnResidual();
        }

        /// <summary>Consumed turn (failed flee, item use, etc): only enemy acts.</summary>
        public void EnemyOnlyTurn()
        {
            if (Result != BattleResult.Ongoing) return;
            var em = EnemyMon;
            var pm = PlayerMon;
            if (em == null || pm == null) return;
            var mv = MoveDB.Get(EnemyChooseMove(em));
            if (mv == null) mv = MoveDB.Get("tackle");
            Execute(em, pm, mv, false);
            EndOfTurnResidual();
        }

        public bool TryCatch(string coreItemId)
        {
            if (Result != BattleResult.Ongoing) return false;
            if (IsTrainerBattle) { Emit("You can't catch another trainer's creature!"); return false; }
            var em = EnemyMon;
            if (em == null || em.Species == null) return false;

            var core = ItemDB.Get(coreItemId);
            int bonus = core != null ? core.catchBonus : 1;

            float hpFactor = (3f * em.MaxHP - 2f * em.curHP) / (3f * em.MaxHP);
            float rate = (em.Species.catchRate / 255f) * hpFactor * (0.35f + 0.2f * bonus) * 2.2f;
            bool ok = UnityEngine.Random.value < Mathf.Clamp01(rate);

            if (ok)
            {
                Emit("The core seals! " + em.DisplayName + " was caught!");
                catchSucceeded = true;
                Result = BattleResult.Caught;
            }
            else
            {
                Emit("The core wobbles... " + em.DisplayName + " broke free!");
            }
            return ok;
        }

        public bool TryFlee()
        {
            if (Result != BattleResult.Ongoing) return false;
            if (IsTrainerBattle) { Emit("There's no running from a trainer battle!"); return false; }
            var pm = PlayerMon;
            var em = EnemyMon;
            // faster than the foe ~always escapes; slower gets 40% + level factor
            float odds = 0.4f;
            if (pm != null && em != null)
                odds = pm.SPD >= em.SPD ? 0.95f : 0.4f + 0.2f * (pm.level - em.level) / 10f;
            odds = Mathf.Clamp(odds, 0.25f, 0.95f);
            if (UnityEngine.Random.value < odds)
            {
                Emit("You slipped away safely.");
                Result = BattleResult.Fled;
                return true;
            }
            Emit("Couldn't escape!");
            return false;
        }

        // ---------------- turn internals ----------------

        void Execute(Creature atk, Creature def, MoveDef mv, bool atkIsPlayer)
        {
            if (atk == null || def == null || mv == null) return;
            if (atk.curHP <= 0 || def.curHP <= 0) return;
            if (!CanAct(atk)) return;

            int pp = atk.CurrentPP(mv.id);
            if (pp <= 0)
            {
                Emit(atk.DisplayName + " has no PP left for " + mv.name + "! It flails!");
                mv = MoveDB.Get("tackle");
            }
            else
            {
                atk.SetPP(mv.id, pp - 1);
            }

            Emit(atk.DisplayName + " used " + mv.name + "!");

            if (mv.accuracy > 0 && UnityEngine.Random.Range(0, 100) >= mv.accuracy)
            {
                Emit("It missed!");
                return;
            }

            if (mv.power > 0)
            {
                int dmg = CalcDamage(atk, def, mv);
                def.curHP = Mathf.Max(0, def.curHP - dmg);
                Emit(def.DisplayName + " took " + dmg + " damage.");
                Sfx("hit");

                if (mv.Effect == "drain")
                {
                    int heal = Mathf.Max(1, dmg / 2);
                    atk.curHP = Mathf.Min(atk.MaxHP, atk.curHP + heal);
                    Emit(atk.DisplayName + " drained " + heal + " HP.");
                }
            }

            ApplyEffect(atk, def, mv);

            if (def.curHP <= 0)
            {
                Emit(def.DisplayName + " fainted!");
                Sfx("faint");

                // mastery XP: every KO with an equipped weapon trains its class
                if (saveState != null && PlayerTeam != null && PlayerTeam.Contains(atk))
                    WeaponMastery.AddXp(saveState, WeaponMastery.ClassOf(atk.weapon), WeaponMastery.XpPerKO);

                HandleFaint(def);
            }
        }

        int CalcDamage(Creature atk, Creature def, MoveDef mv)
        {
            float eff = TypeChart.Effectiveness(mv.element, def.Species.element, def.Species.element2);
            float stab = atk.Species.element == mv.element ? 1.5f : 1f;
            float variance = UnityEngine.Random.Range(0.85f, 1.0f);

            float atkStat = atk.ATK * (1f + 0.25f * AtkBuff(atk));
            float defStat = Mathf.Max(1, def.DEF * (1f + 0.25f * DefBuff(def)));

            // weapon mastery: the equipped weapon's class hits harder as it levels
            float mastery = 1f;
            if (saveState != null && PlayerTeam != null && PlayerTeam.Contains(atk))
                mastery = WeaponMastery.AttackMultiplier(saveState, WeaponMastery.ClassOf(atk.weapon));

            // armor mastery: the defender's equipped armor class takes less damage
            float armor = 1f;
            if (saveState != null && PlayerTeam != null && PlayerTeam.Contains(def))
                armor = WeaponMastery.DefenseMultiplier(saveState, WeaponMastery.ClassOf(def.armor));

            float dmg = ((2f * atk.level / 5f + 2f) * mv.power * atkStat / defStat) / 50f + 2f;
            dmg *= eff * stab * variance * mastery * armor;

            string label = TypeChart.Label(eff);
            if (!string.IsNullOrEmpty(label)) Emit(label);

            return Mathf.Max(1, Mathf.RoundToInt(dmg));
        }

        void ApplyEffect(Creature atk, Creature def, MoveDef mv)
        {
            switch (mv.Effect)
            {
                case "buff_atk":
                    atkBuffs[atk.uid] = Mathf.Min(3, AtkBuff(atk) + 1);
                    Emit(atk.DisplayName + "'s attack rose!");
                    break;
                case "buff_def":
                    defBuffs[atk.uid] = Mathf.Min(3, DefBuff(atk) + 1);
                    Emit(atk.DisplayName + "'s defense rose!");
                    break;
                case "debuff_atk":
                    atkBuffs[def.uid] = Mathf.Max(-3, AtkBuff(def) - 1);
                    Emit(def.DisplayName + "'s attack fell!");
                    break;
                case "debuff_def":
                    defBuffs[def.uid] = Mathf.Max(-3, DefBuff(def) - 1);
                    Emit(def.DisplayName + "'s defense fell!");
                    break;
                case "buff_atk_def": // Aegis Guard: the armor's poise sharpens both
                    atkBuffs[atk.uid] = Mathf.Min(3, AtkBuff(atk) + 1);
                    defBuffs[atk.uid] = Mathf.Min(3, DefBuff(atk) + 1);
                    Emit(atk.DisplayName + " braces — attack and defense rose!");
                    break;
                case "heal":
                    int heal = Mathf.Max(1, atk.MaxHP / 3);
                    atk.curHP = Mathf.Min(atk.MaxHP, atk.curHP + heal);
                    Emit(atk.DisplayName + " recovered health.");
                    break;
                case "status":
                    if (string.IsNullOrEmpty(mv.statusId)) break;
                    if (def.status == mv.statusId) break;
                    if (!string.IsNullOrEmpty(def.status)) { Emit(def.DisplayName + " already has a condition."); break; }
                    if (UnityEngine.Random.Range(0, 100) < (mv.statusChance > 0 ? mv.statusChance : 100))
                    {
                        def.status = mv.statusId;
                        def.statusTurns = mv.statusId == "sleep" ? 2 : 0;
                        Emit(def.DisplayName + " got " + mv.statusId + "!");
                    }
                    break;
            }
        }

        int AtkBuff(Creature c) { int v; return c != null && atkBuffs.TryGetValue(c.uid, out v) ? v : 0; }
        int DefBuff(Creature c) { int v; return c != null && defBuffs.TryGetValue(c.uid, out v) ? v : 0; }

        bool CanAct(Creature c)
        {
            if (c.status == "sleep")
            {
                if (c.statusTurns <= 0)
                {
                    c.status = "";
                    Emit(c.DisplayName + " woke up!");
                    return true;
                }
                c.statusTurns--;
                Emit(c.DisplayName + " is fast asleep.");
                return false;
            }
            if (c.status == "paralyze" && UnityEngine.Random.value < 0.25f)
            {
                Emit(c.DisplayName + " is paralyzed and can't move!");
                return false;
            }
            return true;
        }

        void EndOfTurnResidual()
        {
            TickResidual(PlayerMon);
            if (Result == BattleResult.Ongoing) TickResidual(EnemyMon);
        }

        void TickResidual(Creature c)
        {
            if (c == null || c.curHP <= 0) return;
            if (c.status != "poison" && c.status != "burn") return;

            int dmg = Mathf.Max(1, c.MaxHP / 12);
            c.curHP = Mathf.Max(0, c.curHP - dmg);
            Emit(c.DisplayName + " is hurt by " + c.status + ".");

            if (c.curHP <= 0)
            {
                Emit(c.DisplayName + " fainted!");
                HandleFaint(c);
            }
        }

        void HandleFaint(Creature fainted)
        {
            bool wasEnemy = EnemyTeam != null && EnemyTeam.Contains(fainted);

            if (wasEnemy)
            {
                var next = FirstAlive(EnemyTeam);
                if (next != null)
                {
                    ActiveEnemyIdx = EnemyTeam.IndexOf(next);
                    Emit(IsTrainerBattle ? "Trainer sent out " + next.DisplayName + "!" : "");
                    return;
                }
                Result = BattleResult.PlayerWon;
            }
            else
            {
                var next = FirstAlive(PlayerTeam);
                if (next != null)
                {
                    ActivePlayerIdx = PlayerTeam.IndexOf(next);
                    Emit("Go, " + next.DisplayName + "!");
                    return;
                }
                Result = BattleResult.PlayerLost;
            }
        }

        static Creature FirstAlive(List<Creature> team)
        {
            if (team == null) return null;
            foreach (var c in team) if (c != null && c.curHP > 0) return c;
            return null;
        }

        // ---------------- rewards ----------------

        public void ApplyRewards(GameState state)
        {
            if (state == null) return;

            if (catchSucceeded)
            {
                var caught = EnemyMon;
                if (caught != null)
                {
                    if (!state.caughtSpecies.Contains(caught.speciesId)) state.caughtSpecies.Add(caught.speciesId);
                    state.catchCount++;
                    state.SetFlag("caught_any");
                    caught.status = "";
                    caught.statusTurns = 0;
                    if (state.party.Count < 6) state.party.Add(caught);
                    else state.box.Add(caught);
                    Emit(caught.DisplayName + " joined your" + (state.party.Contains(caught) ? " party!" : " box!"));
                    if (CaughtSpecies != null) CaughtSpecies(caught.speciesId);
                }
                state.coins += 5;
                return;
            }

            if (Result != BattleResult.PlayerWon) return;

            state.battleWins++;

            // only the foes actually knocked out grant XP
            int xpGain = 0;
            foreach (var e in EnemyTeam)
                if (e != null && e.Species != null && e.curHP <= 0)
                    xpGain += e.Species.xpYield * e.level / 6 + 4;
            if (xpGain == 0 && EnemyTeam.Count > 0)
            {
                var any = EnemyTeam[0];
                if (any != null && any.Species != null)
                    xpGain = any.Species.xpYield * any.level / 6 + 4;
            }

            if (IsTrainerBattle && Trainer != null)
            {
                state.coins += Trainer.rewardCoins;
                if (!state.defeatedTrainers.Contains(Trainer.id)) state.defeatedTrainers.Add(Trainer.id);
                foreach (var f in Trainer.onDefeatFlags) state.SetFlag(f);
                foreach (var g in Trainer.onDefeatGiveItems) GiveItem(state, g);
                Emit("You earned " + Trainer.rewardCoins + " coins!");
            }
            else
            {
                int coins = 8 + (EnemyMon != null ? EnemyMon.level * 2 : 4);
                state.coins += coins;
                Emit("You picked up " + coins + " coins.");
            }

            foreach (var p in state.party)
                if (p != null && p.curHP > 0) GainXp(p, xpGain);

            // armor mastery: every survivor wearing armor trains its class on victory
            foreach (var p in state.party)
            {
                if (p == null || p.curHP <= 0) continue;
                var armorClass = WeaponMastery.ClassOf(p.armor);
                if (armorClass != WeaponClass.None)
                    WeaponMastery.AddXp(state, armorClass, WeaponMastery.ArmorXpPerWin);
            }
        }

        void GiveItem(GameState state, string give)
        {
            var parts = give.Split(':');
            int n = parts.Length > 1 && int.TryParse(parts[1], out int c) ? c : 1;
            state.AddItem(parts[0], n);
            Toast.Push("Received " + n + "x " + ItemDB.Name(parts[0]));
        }

        void GainXp(Creature c, int xp)
        {
            c.xp += xp;
            while (c.xp >= XpToNext(c.level) && c.level < MaxCreatureLevel)
            {
                c.xp -= XpToNext(c.level);
                c.level++;
                c.curHP = Mathf.Min(c.MaxHP, c.curHP + 6);
                Emit(c.DisplayName + " grew to level " + c.level + "!");
                TryEvolve(c);
            }
        }

        public const int MaxCreatureLevel = 200;

        public static int XpToNext(int level) => 18 + level * 9;

        void TryEvolve(Creature c)
        {
            var sp = c.Species;
            if (sp == null || string.IsNullOrEmpty(sp.evolvesInto) || c.level < sp.evolveLevel) return;

            var evo = CreatureDB.Get(sp.evolvesInto);
            if (evo == null) return;

            string oldName = c.DisplayName;
            c.speciesId = evo.id;
            if (c.nickname == oldName) c.nickname = "";
            c.curHP = Mathf.Min(c.MaxHP, c.curHP + 10);
            MonsterFactory.FillMoves(c, evo);
            Emit("What? " + oldName + " is evolving! ... " + oldName + " evolved into " + evo.name + "!");
            if (Evolved != null) Evolved(c);
        }

        string EnemyChooseMove(Creature em)
        {
            if (em.moves == null || em.moves.Count == 0) return "tackle";

            string best = null;
            int bestP = -1;
            foreach (var id in em.moves)
            {
                var d = MoveDB.Get(id);
                if (d == null || em.CurrentPP(id) <= 0) continue;
                if (d.power > bestP) { bestP = d.power; best = id; }
            }
            if (best == null) return "tackle";
            if (UnityEngine.Random.value < 0.25f)
            {
                // 25% wildcard: any move with PP left
                var withPP = new List<string>();
                foreach (var id in em.moves)
                    if (em.CurrentPP(id) > 0 && MoveDB.Get(id) != null) withPP.Add(id);
                if (withPP.Count > 0) return withPP[UnityEngine.Random.Range(0, withPP.Count)];
            }
            return best;
        }

        void Emit(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return;
            if (Log != null) Log(msg);
        }

        void Sfx(string id)
        {
            if (SfxRequest != null) SfxRequest(id);
        }
    }
}
