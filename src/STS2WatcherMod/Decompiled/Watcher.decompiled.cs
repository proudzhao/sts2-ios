using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Godot.Collections;
using HarmonyLib;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Localization.Fonts;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using MegaCrit.Sts2.Core.Nodes.Screens.StatsScreen;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using MegaCrit.Sts2.Core.Timeline;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.addons.mega_text;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: InternalsVisibleTo("Prophet")]
namespace WatcherMod;
internal static class WatcherFieldAccess
{
	private const BindingFlags AllFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

	private static readonly System.Collections.Generic.Dictionary<(Type, string), FieldInfo> FieldCache = new System.Collections.Generic.Dictionary<(Type, string), FieldInfo>();

	internal static FieldInfo Field(Type type, string name)
	{
		(Type, string) key = (type, name);
		if (!FieldCache.TryGetValue(key, out FieldInfo field))
		{
			for (Type type2 = type; type2 != null; type2 = type2.BaseType)
			{
				field = type2.GetField(name, AllFlags | BindingFlags.DeclaredOnly);
				if (field != null)
				{
					break;
				}
			}
			FieldCache[key] = field;
		}
		return field;
	}
}


public sealed class WatcherStrike_P : WatcherCard
{
	protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Strike };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(6m, ValueProp.Move) };

	public WatcherStrike_P()
		: base(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(choiceContext);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(3m);
	}
}
public sealed class WatcherDefend_P : WatcherCard
{
	public override bool GainsBlock => true;

	protected override HashSet<CardTag> CanonicalTags => new HashSet<CardTag> { CardTag.Defend };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(5m, ValueProp.Move) };

	public WatcherDefend_P()
		: base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(3m);
	}
}
public sealed class WatcherEruption_P : WatcherCard
{
	public override string PortraitPath => "res://images/packed/card_portraits/watcher/eruption.png";

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Wrath>(null)
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(9m, ValueProp.Move) };

	public WatcherEruption_P()
		: base(2, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_fire_burst")
			.Execute(choiceContext);
		await WatcherCombatHelper.EnterWrath(base.Owner, this);
	}

	protected override void OnUpgrade()
	{
		base.EnergyCost.UpgradeBy(-1);
	}
}
public sealed class WatcherVigilance : WatcherCard
{
	public override bool GainsBlock => true;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Calm>(null)
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(8m, ValueProp.Move) };

	public WatcherVigilance()
		: base(2, CardType.Skill, CardRarity.Basic, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
		await WatcherCombatHelper.EnterCalm(base.Owner, this);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(4m);
	}
}
internal static class WatcherSimpleAHelper
{
	public static async Task Scry(PlayerChoiceContext choiceContext, Player owner, int amount)
	{
		List<CardModel> list = PileType.Draw.GetPile(owner).Cards.Take(amount).ToList();
		if (list.Count == 0)
		{
			return;
		}
		CardSelectorPrefs prefs = WatcherCombatHelper.SetCancelable(new CardSelectorPrefs(WatcherCombatHelper.ScrySelectionPrompt, 0, list.Count), value: true);
		foreach (CardModel item in (await CardSelectCmd.FromSimpleGrid(choiceContext, list, owner, prefs)).ToList())
		{
			await CardPileCmd.Add(item, PileType.Discard);
		}
	}

	public static CardType? GetPreviousPlayedCardType(CardModel currentCard)
	{
		if (WatcherCardCompat.GetCombatState(currentCard) == null)
		{
			return null;
		}
		return CombatManager.Instance.History.CardPlaysStarted.LastOrDefault((CardPlayStartedEntry entry) => entry.CardPlay.Card.Owner == currentCard.Owner && entry.CardPlay.Card != currentCard)?.CardPlay.Card.Type;
	}

	public static bool IsTargetAttacking(Creature? target)
	{
		return target?.Monster?.IntendsToAttack == true;
	}
}
public sealed class WatcherCrescendo : WatcherCard
{
	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[2]
	{
		CardKeyword.Retain,
		CardKeyword.Exhaust
	};

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Wrath>(null)
	};

	public WatcherCrescendo()
		: base(1, CardType.Skill, CardRarity.Common, TargetType.None)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherCombatHelper.EnterWrath(base.Owner, this);
	}

	protected override void OnUpgrade()
	{
		base.EnergyCost.UpgradeBy(-1);
	}
}
public sealed class WatcherTranquility : WatcherCard
{
	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[2]
	{
		CardKeyword.Retain,
		CardKeyword.Exhaust
	};

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Calm>(null)
	};

	public WatcherTranquility()
		: base(1, CardType.Skill, CardRarity.Common, TargetType.None)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherCombatHelper.EnterCalm(base.Owner, this);
	}

	protected override void OnUpgrade()
	{
		base.EnergyCost.UpgradeBy(-1);
	}
}
public sealed class WatcherEmptyFist : WatcherCard
{
	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(9m, ValueProp.Move) };

	public WatcherEmptyFist()
		: base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(choiceContext);
		if (WatcherAttackVfxHelper.IsInStance(base.Owner.Creature))
		{
			WatcherAttackVfxHelper.PlayEmptyStance(base.Owner.Creature);
			await WatcherAttackVfxHelper.WaitSeconds(0.1);
		}
		await WatcherCombatHelper.ExitStance(base.Owner);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(5m);
	}
}
public sealed class WatcherEmptyBody : WatcherCard
{
	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(7m, ValueProp.Move) };

	public WatcherEmptyBody()
		: base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
		if (WatcherAttackVfxHelper.IsInStance(base.Owner.Creature))
		{
			WatcherAttackVfxHelper.PlayEmptyStance(base.Owner.Creature);
			await WatcherAttackVfxHelper.WaitSeconds(0.1);
		}
		await WatcherCombatHelper.ExitStance(base.Owner);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(3m);
	}
}
public sealed class WatcherConsecrate : WatcherCard
{
	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(5m, ValueProp.Move) };

	public WatcherConsecrate()
		: base(0, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(base.CombatState, "CombatState");
		WatcherAttackVfxHelper.PlayCleave();
		await WatcherAttackVfxHelper.WaitSeconds(0.1);
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).TargetingAllOpponentsCompat(base.CombatState)
			.Execute(choiceContext);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(3m);
	}
}
public sealed class WatcherFlurryOfBlows : WatcherCard
{
	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(4m, ValueProp.Move) };

	public WatcherFlurryOfBlows()
		: base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(choiceContext);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(2m);
	}
}
public sealed class WatcherProtect : WatcherCard
{
	public override bool GainsBlock => true;

	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Retain };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(12m, ValueProp.Move) };

	public WatcherProtect()
		: base(2, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(4m);
	}
}
public sealed class WatcherProstrate : WatcherCard
{
	public override bool GainsBlock => true;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Mantra>(null)
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[2]
	{
		new BlockVar(4m, ValueProp.Move),
		new DynamicVar("MagicNumber", 2m)
	};

	public WatcherProstrate()
		: base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
		await WatcherCombatHelper.GainMantra(base.Owner, base.DynamicVars["MagicNumber"].IntValue, this);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherBowlingBash : WatcherCard
{
	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(7m, ValueProp.Move) };

	public WatcherBowlingBash()
		: base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		int num = base.CombatState.HittableEnemies.Count();
		if (num > 0)
		{
			await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).WithHitCount(num).FromCard((CardModel)this, cardPlay)
				.Targeting(cardPlay.Target)
				.WithHitFx("vfx/vfx_attack_blunt")
				.Execute(choiceContext);
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(3m);
	}
}
public sealed class WatcherCrushJoints : WatcherCard
{
	protected override bool ShouldGlowGoldInternal => WatcherSimpleAHelper.GetPreviousPlayedCardType(this) == CardType.Skill;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromPower<VulnerablePower>(null) };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[2]
	{
		new DamageVar(8m, ValueProp.Move),
		new PowerVar<VulnerablePower>(1m)
	};

	public WatcherCrushJoints()
		: base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_heavy_blunt")
			.Execute(choiceContext);
		if (WatcherSimpleAHelper.GetPreviousPlayedCardType(this) == CardType.Skill)
		{
			await WatcherPowerCmdCompat.Apply<VulnerablePower>(cardPlay.Target, base.DynamicVars.Vulnerable.BaseValue, base.Owner.Creature, this);
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(2m);
		base.DynamicVars.Vulnerable.UpgradeValueBy(1m);
	}
}
public sealed class WatcherCutThroughFate : WatcherCard
{
	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[2]
	{
		new DamageVar(7m, ValueProp.Move),
		new DynamicVar("MagicNumber", 2m)
	};

	public WatcherCutThroughFate()
		: base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_flying_slash")
			.Execute(choiceContext);
		await WatcherCombatHelper.Scry(choiceContext, base.Owner, base.DynamicVars["MagicNumber"].IntValue, this);
		await CardPileCmd.Draw(choiceContext, 1m, base.Owner);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(2m);
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherEvaluate : WatcherCard
{
	public override bool GainsBlock => true;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromCard<WatcherInsight>() };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(6m, ValueProp.Move) };

	public WatcherEvaluate()
		: base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
		CardModel card = base.CombatState.CreateCard<WatcherInsight>(base.Owner);
		if (base.IsUpgraded)
		{
			CardCmd.Upgrade(card);
		}
		await WatcherCardPileCompat.AddGeneratedCardToCombat(card, PileType.Draw, addedByPlayer: true, CardPilePosition.Random);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(4m);
	}
}
public sealed class WatcherFlyingSleeves : WatcherCard
{
	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Retain };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(4m, ValueProp.Move) };

	public WatcherFlyingSleeves()
		: base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		int hitIndex = 0;
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).WithHitCount(2).FromCard((CardModel)this, cardPlay)
			.Targeting(cardPlay.Target)
			.BeforeDamage(delegate
			{
				WatcherAttackVfxHelper.PlayFlyingSleevesSlash(cardPlay.Target, hitIndex++ == 1);
				return Task.CompletedTask;
			})
			.Execute(choiceContext);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(2m);
	}
}
public sealed class WatcherFollowUp : WatcherCard
{
	protected override bool ShouldGlowGoldInternal => WatcherSimpleAHelper.GetPreviousPlayedCardType(this) == CardType.Attack;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { base.EnergyHoverTip };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[2]
	{
		new DamageVar(7m, ValueProp.Move),
		new EnergyVar(1)
	};

	public WatcherFollowUp()
		: base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(choiceContext);
		if (WatcherSimpleAHelper.GetPreviousPlayedCardType(this) == CardType.Attack)
		{
			await PlayerCmd.GainEnergy(base.DynamicVars.Energy.BaseValue, base.Owner);
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(4m);
	}
}
public sealed class WatcherHalt : WatcherCard
{
	public override bool GainsBlock => true;

	protected override bool ShouldGlowGoldInternal
	{
		get
		{
			if (base.Owner != null)
			{
				return WatcherCombatHelper.IsInStance<Wrath>(base.Owner.Creature);
			}
			return false;
		}
	}

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[2]
	{
		new BlockVar(3m, ValueProp.Move),
		new BlockVar("MagicNumber", 9m, ValueProp.Move)
	};

	public WatcherHalt()
		: base(0, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
		if (WatcherCombatHelper.IsInStance<Wrath>(base.Owner.Creature))
		{
			await CreatureCmd.GainBlock(base.Owner.Creature, (BlockVar)base.DynamicVars["MagicNumber"], cardPlay);
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(1m);
		base.DynamicVars["MagicNumber"].UpgradeValueBy(4m);
	}
}
public class WatcherJustLucky : WatcherCard
{
	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[3]
	{
		new DamageVar(3m, ValueProp.Move),
		new BlockVar(2m, ValueProp.Move),
		new DynamicVar("MagicNumber", 1m)
	};

	public WatcherJustLucky()
		: base(0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await WatcherCombatHelper.Scry(choiceContext, base.Owner, base.DynamicVars["MagicNumber"].IntValue, this);
		WatcherAttackVfxHelper.PlayFlickCoin(base.Owner.Creature, cardPlay.Target);
		await WatcherAttackVfxHelper.WaitSeconds(0.3);
		await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.Execute(choiceContext);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(1m);
		base.DynamicVars.Block.UpgradeValueBy(1m);
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherSashWhip : WatcherCard
{
	protected override bool ShouldGlowGoldInternal => WatcherSimpleAHelper.GetPreviousPlayedCardType(this) == CardType.Attack;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromPower<WeakPower>(null) };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[2]
	{
		new DamageVar(8m, ValueProp.Move),
		new PowerVar<WeakPower>("MagicNumber", 1m)
	};

	public WatcherSashWhip()
		: base(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(choiceContext);
		if (WatcherSimpleAHelper.GetPreviousPlayedCardType(this) == CardType.Attack)
		{
			await WatcherPowerCmdCompat.Apply<WeakPower>(cardPlay.Target, base.DynamicVars["MagicNumber"].BaseValue, base.Owner.Creature, this);
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(2m);
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public class WatcherThirdEye : WatcherCard
{
	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[2]
	{
		new BlockVar(7m, ValueProp.Move),
		new DynamicVar("MagicNumber", 3m)
	};

	public WatcherThirdEye()
		: base(1, CardType.Skill, CardRarity.Common, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
		await WatcherCombatHelper.Scry(choiceContext, base.Owner, base.DynamicVars["MagicNumber"].IntValue, this);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(2m);
		base.DynamicVars["MagicNumber"].UpgradeValueBy(2m);
	}
}
public sealed class WatcherPressurePoints : WatcherCard
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromPower<MarkPower>(null) };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 8m) };

	public WatcherPressurePoints()
		: base(1, CardType.Skill, CardRarity.Common, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await WatcherPowerCmdCompat.Apply<MarkPower>(cardPlay.Target, base.DynamicVars["MagicNumber"].BaseValue, base.Owner.Creature, this);
		foreach (Creature item in base.CombatState.HittableEnemies.ToList())
		{
			int powerAmount = item.GetPowerAmount<MarkPower>();
			if (powerAmount > 0)
			{
				await CreatureCmd.Damage(choiceContext, item, (decimal)powerAmount, ValueProp.Unblockable | ValueProp.Unpowered, base.Owner.Creature, (CardModel)this, cardPlay);
			}
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(3m);
	}
}
public sealed class WatcherDevotion : WatcherCard
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[3]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<DevotionPower>(null),
		HoverTipFactory.FromPower<Mantra>(null)
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 2m) };

	public WatcherDevotion()
		: base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(base.Owner.Creature, "Cast", base.Owner.Character.CastAnimDelay);
		await WatcherPowerCmdCompat.Apply<DevotionPower>(base.Owner.Creature, base.DynamicVars["MagicNumber"].BaseValue, base.Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherAlpha : WatcherCard
{
	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromCard<WatcherBeta>() };

	public WatcherAlpha()
		: base(1, CardType.Skill, CardRarity.Rare, TargetType.None)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherCardPileCompat.AddGeneratedCardToCombat(base.CombatState.CreateCard<WatcherBeta>(base.Owner), PileType.Draw, addedByPlayer: true, CardPilePosition.Random);
	}

	protected override void OnUpgrade()
	{
		AddKeyword(CardKeyword.Innate);
	}
}
internal sealed class BrillianceDamageVar : DamageVar
{
	public BrillianceDamageVar(decimal damage, ValueProp props)
		: base(damage, props)
	{
	}

	public override void UpdateCardPreview(CardModel card, CardPreviewMode previewMode, Creature? target, bool runGlobalHooks)
	{
		decimal num = ((decimal?)card.Owner?.Creature?.GetPower<WatcherStatePower>()?.TotalMantraGainedThisCombat) ?? 0m;
		decimal num2 = base.BaseValue + num;
		decimal num3 = num2;
		EnchantmentModel enchantment = card.Enchantment;
		if (enchantment != null)
		{
			num3 += enchantment.EnchantDamageAdditive(num3, base.Props);
			num3 *= enchantment.EnchantDamageMultiplicative(num3, base.Props);
			if (!card.IsEnchantmentPreview)
			{
				base.EnchantedValue = num3;
			}
		}
		CombatState combatState = WatcherCardCompat.GetCombatState(card);
		if (runGlobalHooks && combatState != null)
		{
			num3 = WatcherHookCompat.ModifyDamage(card.Owner.RunState, combatState, target, card.Owner.Creature, num2, base.Props, card, ModifyDamageHookType.All, previewMode, out IEnumerable<AbstractModel> _);
		}
		else
		{
			List<EnchantmentModel> extras = WatcherEnchantStack.GetExtras(card);
			if (extras != null && extras.Count > 0)
			{
				foreach (EnchantmentModel item in extras)
				{
					num3 += item.EnchantDamageAdditive(num3, base.Props);
					num3 *= item.EnchantDamageMultiplicative(num3, base.Props);
				}
				if (!card.IsEnchantmentPreview)
				{
					base.EnchantedValue = num3;
				}
			}
		}
		base.PreviewValue = num3;
	}
}
public sealed class WatcherBrilliance : WatcherCard
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Mantra>(null)
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BrillianceDamageVar(12m, ValueProp.Move) };

	public WatcherBrilliance()
		: base(1, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		decimal num = WatcherCombatHelper.GetTotalMantraGained(base.Owner);
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue + num).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_starry_impact")
			.Execute(choiceContext);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(4m);
	}
}
public sealed class WatcherDeusExMachina : WatcherCard
{
	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[2]
	{
		CardKeyword.Unplayable,
		CardKeyword.Exhaust
	};

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromCard<WatcherMiracle>() };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 2m) };

	public WatcherDeusExMachina()
		: base(-1, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
	{
		if (card != this || base.CombatState == null)
		{
			return;
		}
		CardPile? pile = base.Pile;
		if (pile == null || pile.Type != PileType.Hand)
		{
			return;
		}
		await CardCmd.AutoPlay(choiceContext, this, null);
		CardPile? pile2 = base.Pile;
		if (pile2 != null && pile2.Type == PileType.Exhaust)
		{
			for (int index = 0; index < base.DynamicVars["MagicNumber"].IntValue; index++)
			{
				await WatcherCardPileCompat.AddGeneratedCardToCombat(base.CombatState.CreateCard<WatcherMiracle>(base.Owner), PileType.Hand, addedByPlayer: true);
			}
		}
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await Task.CompletedTask;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public class WatcherWish_P : WatcherCard
{
	public override string PortraitPath => "res://images/packed/card_portraits/watcher/wish.png";

	public override bool CanBeGeneratedInCombat => false;

	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 25m) };

	public WatcherWish_P()
		: base(3, CardType.Skill, CardRarity.Rare, TargetType.None)
	{
	}

	protected virtual Task<bool> TryConsumeKnowFateBoost()
	{
		return Task.FromResult(result: false);
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		bool flag = await TryConsumeKnowFateBoost();
		CardModel optStrength = base.CombatState.CreateCard<WatcherWishAlmighty>(base.Owner);
		CardModel optArmor = base.CombatState.CreateCard<WatcherWishLiveForever>(base.Owner);
		CardModel optGold = base.CombatState.CreateCard<WatcherWishFameAndFortune>(base.Owner);
		bool boosted = base.IsUpgraded || flag;
		if (boosted)
		{
			optStrength.UpgradeInternal();
			optArmor.UpgradeInternal();
			optGold.UpgradeInternal();
		}
		List<CardModel> options = new List<CardModel>(3) { optStrength, optArmor, optGold };
		CardModel cardModel = await WatcherCombatHelper.ChooseOne(choiceContext, base.Owner, options, new LocString("cards", "WATCHER_WISH_P.selectionScreenPrompt"));
		if (cardModel == optStrength)
		{
			int num = (boosted ? 4 : 3);
			await WatcherPowerCmdCompat.Apply<StrengthPower>(base.Owner.Creature, num, base.Owner.Creature, this);
		}
		else if (cardModel == optArmor)
		{
			int num2 = (boosted ? 8 : 6);
			await WatcherPowerCmdCompat.Apply<WishPlatedArmorPower>(base.Owner.Creature, num2, base.Owner.Creature, this);
		}
		else if (cardModel == optGold)
		{
			await PlayerCmd.GainGold(boosted ? 30 : 25, base.Owner);
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(5m);
	}
}
public sealed class WishPlatedArmorPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		if (side == base.Owner.Side)
		{
			Flash();
			await CreatureCmd.GainBlock(base.Owner, base.Amount, ValueProp.Unpowered, null);
		}
	}

	public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (target == base.Owner && result.UnblockedDamage > 0)
		{
			await WatcherPowerCmdCompat.ModifyAmount(this, -1m, null, null);
			if (base.Amount <= 0)
			{
				await PowerCmd.Remove(this);
			}
		}
	}
}
public sealed class WatcherVault : WatcherCard
{
	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

	public WatcherVault()
		: base(3, CardType.Skill, CardRarity.Rare, TargetType.None)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherCombatHelper.TakeExtraTurn(base.Owner);
	}

	protected override void OnUpgrade()
	{
		base.EnergyCost.UpgradeBy(-1);
	}
}
public sealed class WatcherConjureBlade : WatcherCard
{
	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

	protected override bool HasEnergyCostX => true;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromCard<WatcherExpunger>() };

	public WatcherConjureBlade()
		: base(-1, CardType.Skill, CardRarity.Rare, TargetType.None)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		int num = ResolveEnergyXValue();
		if (base.IsUpgraded)
		{
			num++;
		}
		if (num > 0)
		{
			WatcherExpunger watcherExpunger = base.CombatState.CreateCard<WatcherExpunger>(base.Owner);
			if (watcherExpunger is WatcherExpunger watcherExpunger2)
			{
				watcherExpunger2.HitCount = num;
			}
			await WatcherCardPileCompat.AddGeneratedCardToCombat(watcherExpunger, PileType.Draw, addedByPlayer: true, CardPilePosition.Random);
		}
	}

	protected override void OnUpgrade()
	{
	}
}
public sealed class WatcherOmniscience : WatcherCard
{
	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

	public WatcherOmniscience()
		: base(4, CardType.Skill, CardRarity.Rare, TargetType.None)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		List<CardModel> list = PileType.Draw.GetPile(base.Owner).Cards.ToList();
		if (list.Count == 0)
		{
			return;
		}
		CardSelectorPrefs prefs = WatcherCombatHelper.SetCancelable(new CardSelectorPrefs(new LocString("card_selection", "CHOOSE_A_CARD"), 1), value: false);
		CardModel chosenCard = (await CardSelectCmd.FromSimpleGrid(choiceContext, list, base.Owner, prefs)).FirstOrDefault();
		if (chosenCard == null)
		{
			return;
		}
		await CardPileCmd.Add(chosenCard, PileType.Play);
		if (WatcherCombatHelper.IsBlockedByCardLogic(chosenCard))
		{
			await CardCmd.Exhaust(choiceContext, chosenCard, false, false);
			return;
		}
		await WatcherPowerCmdCompat.Apply<OmniscienceDoublePower>(base.Owner.Creature, 1m, base.Owner.Creature, this);
		await CardCmd.AutoPlay(choiceContext, chosenCard, null);
		if (WatcherCardCompat.GetCombatState(chosenCard) != null)
		{
			CardPile? pile = chosenCard.Pile;
			if (pile == null || pile.Type != PileType.Exhaust)
			{
				await CardCmd.Exhaust(choiceContext, chosenCard, false, false);
			}
		}
	}

	protected override void OnUpgrade()
	{
		base.EnergyCost.UpgradeBy(-1);
	}
}
public sealed class WatcherBlasphemy : WatcherCard
{
	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[3]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Divinity>(null),
		HoverTipFactory.FromPower<EndTurnDeathPower>(null)
	};

	public WatcherBlasphemy()
		: base(1, CardType.Skill, CardRarity.Rare, TargetType.None)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		WatcherAttackVfxHelper.PlayBlasphemyEyeOpen(base.Owner.Creature);
		await WatcherCombatHelper.EnterDivinity(base.Owner, this);
		await WatcherPowerCmdCompat.Apply<EndTurnDeathPower>(base.Owner.Creature, 1m, base.Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		AddKeyword(CardKeyword.Retain);
	}
}
public sealed class WatcherJudgment : WatcherCard
{
	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 30m) };

	public WatcherJudgment()
		: base(1, CardType.Skill, CardRarity.Rare, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		Creature target = cardPlay.Target;
		bool fatal = target.CurrentHp <= base.DynamicVars["MagicNumber"].IntValue;
		WatcherAttackVfxHelper.PlayJudgment(base.Owner.Creature, target, base.TitleLocString.GetFormattedText());
		await WatcherAttackVfxHelper.WaitRealSeconds(0.5200000405311584);
		if (fatal)
		{
			WatcherJudgmentDeath.Mark(target);
			try
			{
				await CreatureCmd.Kill(target);
			}
			finally
			{
				WatcherJudgmentDeath.Unmark(target);
			}
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(10m);
	}
}
public sealed class WatcherLessonLearned : WatcherCard
{
	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

	public override bool CanBeGeneratedInCombat => false;

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(10m, ValueProp.Move) };

	public WatcherLessonLearned()
		: base(2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		bool shouldTriggerFatal = cardPlay.Target.Powers.All((PowerModel p) => p.ShouldOwnerDeathTriggerFatal());
		AttackCommand command = await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_starry_impact")
			.Execute(choiceContext);
		if (!shouldTriggerFatal || !WatcherAttackCommandCompat.GetDamageResults(command).Any((DamageResult r) => r.WasTargetKilled))
		{
			return;
		}
		List<CardModel> list = PileType.Deck.GetPile(base.Owner).Cards.Where((CardModel c) => c.IsUpgradable).ToList();
		if (list.Count > 0)
		{
			CardModel cardModel = base.Owner.RunState.Rng.Niche.NextItem(list);
			base.Owner.RunState.CurrentMapPointHistoryEntry?.GetEntry(base.Owner.NetId).UpgradedCards.Add(cardModel.Id);
			cardModel.UpgradeInternal();
			cardModel.FinalizeUpgradeInternal();
			if (LocalContext.IsMe(base.Owner))
			{
				NRun.Instance?.GlobalUi.CardPreviewContainer.AddChildSafely(NCardSmithVfx.Create(new CardModel[1] { cardModel }));
			}
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(3m);
	}
}
public sealed class WatcherRagnarok : WatcherCard
{
	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[2]
	{
		new DamageVar(5m, ValueProp.Move),
		new DynamicVar("MagicNumber", 5m)
	};

	public WatcherRagnarok()
		: base(3, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(base.CombatState, "CombatState");
		int intValue = base.DynamicVars["MagicNumber"].IntValue;
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).WithHitCount(intValue).FromCard((CardModel)this, cardPlay)
			.TargetingRandomOpponentsCompat(base.CombatState)
			.WithHitFx("vfx/vfx_attack_lightning")
			.BeforeDamage(delegate
			{
				WatcherAudioHelper.PlayOneShot("res://audio/combat/lightning_evoke.ogg", 0.1f);
				return Task.CompletedTask;
			})
			.Execute(choiceContext);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(1m);
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherScrawl_P : WatcherCard
{
	public override string PortraitPath => "res://images/packed/card_portraits/watcher/scrawl.png";

	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

	public WatcherScrawl_P()
		: base(1, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		int count = PileType.Hand.GetPile(base.Owner).Cards.Count;
		int num = 10 - count;
		if (num > 0)
		{
			await CardPileCmd.Draw(choiceContext, num, base.Owner);
		}
	}

	protected override void OnUpgrade()
	{
		base.EnergyCost.UpgradeBy(-1);
	}
}
public sealed class WatcherSpiritShield : WatcherCard
{
	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 3m) };

	public WatcherSpiritShield()
		: base(2, CardType.Skill, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		decimal num = (decimal)PileType.Hand.GetPile(base.Owner).Cards.Count * base.DynamicVars["MagicNumber"].BaseValue;
		if (num > 0m)
		{
			await CreatureCmd.GainBlock(base.Owner.Creature, num, ValueProp.Move, cardPlay);
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherDevaForm : WatcherCard
{
	public override IEnumerable<CardKeyword> CanonicalKeywords
	{
		get
		{
			if (!base.IsUpgraded)
			{
				return new CardKeyword[] { CardKeyword.Ethereal };
			}
			return System.Array.Empty<CardKeyword>();
		}
	}

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 1m) };

	public WatcherDevaForm()
		: base(3, CardType.Power, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		DevaPower power = base.Owner.Creature.GetPower<DevaPower>();
		if (power == null)
		{
			await WatcherPowerCmdCompat.Apply<DevaPower>(base.Owner.Creature, 1m, base.Owner.Creature, this);
		}
		else
		{
			power.AddInstance(1);
		}
		WatcherAttackVfxHelper.StartDevaEyeSweep(base.Owner.Creature);
	}

	protected override void OnUpgrade()
	{
		RemoveKeyword(CardKeyword.Ethereal);
	}
}
public sealed class WatcherEstablishment : WatcherCard
{
	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 1m) };

	public WatcherEstablishment()
		: base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherPowerCmdCompat.Apply<EstablishmentPower>(base.Owner.Creature, base.DynamicVars["MagicNumber"].BaseValue, base.Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		AddKeyword(CardKeyword.Innate);
	}
}
public sealed class WatcherMasterReality : WatcherCard
{
	public WatcherMasterReality()
		: base(1, CardType.Power, CardRarity.Rare, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherPowerCmdCompat.Apply<MasterRealityPower>(base.Owner.Creature, 1m, base.Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		base.EnergyCost.UpgradeBy(-1);
	}
}
public sealed class WatcherCataclysm : WatcherCard
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Wrath>(null)
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[2]
	{
		new DamageVar(8m, ValueProp.Move),
		new RepeatVar(2)
	};

	public WatcherCataclysm()
		: base(2, CardType.Attack, CardRarity.Ancient, TargetType.AllEnemies)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(base.CombatState, "CombatState");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).WithHitCount(base.DynamicVars.Repeat.IntValue).FromCard((CardModel)this, cardPlay)
			.TargetingAllOpponentsCompat(base.CombatState)
			.WithHitFx("vfx/vfx_attack_slash")
			.SpawningHitVfxOnEachCreature()
			.Execute(choiceContext);
		await WatcherCombatHelper.EnterWrath(base.Owner, this);
	}

	protected override void OnUpgrade()
	{
		base.EnergyCost.UpgradeBy(-1);
		base.DynamicVars.Damage.UpgradeValueBy(2m);
	}
}
public sealed class WatcherSerenity : WatcherCard
{
	public override bool GainsBlock => true;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[3]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Calm>(null),
		HoverTipFactory.FromPower<RestfulPower>(null)
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(12m, ValueProp.Move) };

	public WatcherSerenity()
		: base(2, CardType.Skill, CardRarity.Ancient, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
		await WatcherPowerCmdCompat.SetAmount<RestfulPower>(base.Owner.Creature, 25m, base.Owner.Creature, this);
		await WatcherCombatHelper.EnterCalm(base.Owner, this);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(4m);
		AddKeyword(CardKeyword.Retain);
	}
}
public sealed class WatcherPreach : WatcherCard
{
	private bool IsMultiplayer => (base.RunState?.Players.Count ?? 1) > 1;

	private IHoverTip GospelHoverTip
	{
		get
		{
			LocString locString = new LocString("powers", "GOSPEL_POWER.smartDescription");
			locString.Add("Amount", 1m);
			locString.Add("IsMultiplayer", IsMultiplayer);
			return new HoverTip(new LocString("powers", "GOSPEL_POWER.title"), locString);
		}
	}

	protected override IEnumerable<IHoverTip> ExtraHoverTips
	{
		get
		{
			if (!IsMultiplayer)
			{
				return new IHoverTip[4]
				{
					HoverTipFactory.FromPower<DevotionPower>(null),
					GospelHoverTip,
					HoverTipFactory.FromPower<Mantra>(null),
					WatcherHoverTips.Stance
				};
			}
			return new IHoverTip[6]
			{
				HoverTipFactory.FromPower<DevotionPower>(null),
				GospelHoverTip,
				HoverTipFactory.FromPower<DivineDoomPower>(null),
				HoverTipFactory.FromPower<Mantra>(null),
				WatcherHoverTips.Stance,
				HoverTipFactory.FromPower<DoomPower>(null)
			};
		}
	}

	public WatcherPreach()
		: base(3, CardType.Power, CardRarity.Ancient, TargetType.Self)
	{
	}

	protected override void AddExtraArgsToDescription(LocString description)
	{
		description.Add("IsMultiplayer", IsMultiplayer);
		string variable;
		switch (LocManager.Instance?.Language ?? "eng")
		{
		case "zhs":
		case "zht":
		case "jpn":
		case "kor":
			variable = (IsMultiplayer ? "和[gold]天罚[/gold]" : "");
			break;
		default:
			variable = (IsMultiplayer ? " and [gold]Divine Judgment[/gold]" : "");
			break;
		}
		description.Add("DoomClause", variable);
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherPowerCmdCompat.Apply<DevotionPower>(base.Owner.Creature, 2m, base.Owner.Creature, this);
		await WatcherPowerCmdCompat.Apply<GospelPower>(base.Owner.Creature, 1m, base.Owner.Creature, this);
		if (IsMultiplayer)
		{
			await WatcherPowerCmdCompat.Apply<DivineDoomPower>(base.Owner.Creature, 1m, base.Owner.Creature, this);
		}
	}

	protected override void OnUpgrade()
	{
		base.EnergyCost.UpgradeBy(-1);
		AddKeyword(CardKeyword.Innate);
	}
}
public sealed class WatcherDrawTalisman : WatcherCard
{
	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 1m) };

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		WatcherHoverTips.Enchantment,
		WatcherHoverTips.Directed
	};

	public WatcherDrawTalisman()
		: base(4, CardType.Skill, CardRarity.Ancient, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		PlayerCombatState combat = base.Owner.PlayerCombatState;
		if (combat == null)
		{
			return;
		}
		IReadOnlyList<EnchantmentModel> pool = WatcherEnchantStack.RandomPool;
		if (pool.Count == 0)
		{
			return;
		}
		int times = base.DynamicVars["MagicNumber"].IntValue;
		CardModel optRandom = base.CombatState.CreateCard<WatcherDrawTalismanRandomDeck>(base.Owner);
		CardModel cardModel = base.CombatState.CreateCard<WatcherDrawTalismanDirectedHand>(base.Owner);
		CardModel cardModel2 = await WatcherCombatHelper.ChooseOne(choiceContext, base.Owner, new CardModel[2] { optRandom, cardModel }, new LocString("cards", "WATCHER_DRAW_TALISMAN.selectionScreenPrompt"));
		if (cardModel2 == null)
		{
			return;
		}
		ulong seed = base.Owner.RunState.Rng.Seed;
		int num = WatcherCreatureCompat.GetCombatState(base.Owner.Creature)?.RoundNumber ?? 0;
		Rng rng = new Rng(seed ^ (uint)(num * 2654435761u));
		List<CardModel> list;
		bool flag;
		if (cardModel2 == optRandom)
		{
			list = (from c in combat.AllPiles.SelectMany((CardPile p) => p.Cards)
				where c != this
				select c).ToList();
			flag = false;
		}
		else
		{
			list = combat.Hand.Cards.Where((CardModel c) => c != this).ToList();
			flag = true;
		}
		foreach (CardModel card in list)
		{
			for (int i = 0; i < times; i++)
			{
				IReadOnlyList<EnchantmentModel> readOnlyList;
				if (!flag)
				{
					readOnlyList = pool;
				}
				else
				{
					IReadOnlyList<EnchantmentModel> readOnlyList2 = pool.Where((EnchantmentModel e) => WatcherEnchantStack.CanApplyTempEnchantmentTo(card, e, directed: true)).ToList();
					readOnlyList = readOnlyList2;
				}
				IReadOnlyList<EnchantmentModel> readOnlyList3 = readOnlyList;
				if (readOnlyList3.Count != 0)
				{
					EnchantmentModel canonical = readOnlyList3[rng.NextInt(readOnlyList3.Count)];
					WatcherEnchantStack.ApplyTempEnchantment(card, canonical);
				}
			}
		}
	}

	protected override void OnUpgrade()
	{
		base.EnergyCost.UpgradeBy(-1);
		AddKeyword(CardKeyword.Retain);
	}
}
public sealed class WatcherRelinquish : WatcherCard
{
	public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

	public WatcherRelinquish()
		: base(2, CardType.Skill, CardRarity.Rare, TargetType.AllAllies)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		IEnumerable<Creature> enumerable = from c in base.CombatState.GetTeammatesOf(base.Owner.Creature)
			where c != null && c.IsAlive && c.IsPlayer && c != base.Owner.Creature
			select c;
		foreach (Creature item in enumerable)
		{
			await WatcherPowerCmdCompat.Apply<WatcherExtraTurnPower>(item, 1m, item, this);
		}
		await WatcherCombatHelper.EndTurnSafely(base.Owner);
	}

	protected override void OnUpgrade()
	{
		AddKeyword(CardKeyword.Retain);
	}
}
public sealed class WatcherSanctification : WatcherCard
{
	public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[3]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Divinity>(null),
		HoverTipFactory.FromPower<EndTurnDeathPower>(null)
	};

	public WatcherSanctification()
		: base(1, CardType.Skill, CardRarity.Rare, TargetType.AllAllies)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		IEnumerable<Creature> enumerable = from c in base.CombatState.GetTeammatesOf(base.Owner.Creature)
			where c != null && c.IsAlive && c.IsPlayer && c != base.Owner.Creature
			select c;
		foreach (Creature item in enumerable)
		{
			if (item.Player != null)
			{
				await WatcherCombatHelper.EnterDivinity(item.Player, this);
			}
		}
		await WatcherPowerCmdCompat.Apply<EndTurnDeathPower>(base.Owner.Creature, 1m, base.Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		AddKeyword(CardKeyword.Retain);
	}
}
public sealed class WatcherMiracle : WatcherCard
{
	public override CardPoolModel Pool => ModelDb.CardPool<TokenCardPool>();

	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[2]
	{
		CardKeyword.Retain,
		CardKeyword.Exhaust
	};

	public override bool CanBeGeneratedInCombat => false;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { base.EnergyHoverTip };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new EnergyVar(1) };

	public WatcherMiracle()
		: base(0, CardType.Skill, CardRarity.Token, TargetType.None)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await PlayerCmd.GainEnergy(base.DynamicVars.Energy.BaseValue, base.Owner);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Energy.UpgradeValueBy(1m);
	}
}
public sealed class OmegaPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		CombatState combatState = WatcherCreatureCompat.GetCombatState(base.Owner);
		if (side == base.Owner.Side && combatState != null)
		{
			Flash();
			await CreatureCmd.Damage(choiceContext, (IEnumerable<Creature>)combatState.HittableEnemies, (decimal)base.Amount, ValueProp.Unpowered, base.Owner, (CardModel)null, (CardPlay)null);
		}
	}
}
public sealed class WatcherBeta : WatcherCard
{
	public override CardPoolModel Pool => ModelDb.CardPool<TokenCardPool>();

	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

	public WatcherBeta()
		: base(2, CardType.Skill, CardRarity.Token, TargetType.None)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherCardPileCompat.AddGeneratedCardToCombat(base.CombatState.CreateCard<WatcherOmega>(base.Owner), PileType.Draw, addedByPlayer: true, CardPilePosition.Random);
	}

	protected override void OnUpgrade()
	{
		base.EnergyCost.UpgradeBy(-1);
	}
}
public sealed class WatcherOmega : WatcherCard
{
	public override CardPoolModel Pool => ModelDb.CardPool<TokenCardPool>();

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 50m) };

	public WatcherOmega()
		: base(3, CardType.Power, CardRarity.Token, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherPowerCmdCompat.Apply<OmegaPower>(base.Owner.Creature, base.DynamicVars["MagicNumber"].BaseValue, base.Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(10m);
	}
}
public sealed class WatcherSafety : WatcherCard
{
	public override CardPoolModel Pool => ModelDb.CardPool<TokenCardPool>();

	public override bool GainsBlock => true;

	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[2]
	{
		CardKeyword.Retain,
		CardKeyword.Exhaust
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(12m, ValueProp.Move) };

	public WatcherSafety()
		: base(1, CardType.Skill, CardRarity.Token, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(4m);
	}
}
public sealed class WatcherThroughViolence : WatcherCard
{
	public override CardPoolModel Pool => ModelDb.CardPool<TokenCardPool>();

	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[2]
	{
		CardKeyword.Retain,
		CardKeyword.Exhaust
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(20m, ValueProp.Move) };

	public WatcherThroughViolence()
		: base(0, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		WatcherAttackVfxHelper.PlayViolentAttack(cardPlay.Target, new Color(32f / 51f, 0.12549f, 0.941176f));
		await WatcherAttackVfxHelper.WaitSeconds(0.4);
		WatcherAttackVfxHelper.PlayViolentAttack(cardPlay.Target, new Color(1f, 0f, 0f));
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.Execute(choiceContext);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(10m);
	}
}
public sealed class WatcherWishAlmighty : WatcherCard
{
	public override CardPoolModel Pool => ModelDb.CardPool<ColorlessCardPool>();

	public override string PortraitPath => "res://images/packed/card_portraits/watcher/become_almighty.png";

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 3m) };

	public WatcherWishAlmighty()
		: base(-2, CardType.Power, CardRarity.Token, TargetType.None)
	{
	}

	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return Task.CompletedTask;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherWishLiveForever : WatcherCard
{
	public override CardPoolModel Pool => ModelDb.CardPool<ColorlessCardPool>();

	public override string PortraitPath => "res://images/packed/card_portraits/watcher/live_forever.png";

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 6m) };

	public WatcherWishLiveForever()
		: base(-2, CardType.Power, CardRarity.Token, TargetType.None)
	{
	}

	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return Task.CompletedTask;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(2m);
	}
}
public sealed class WatcherWishFameAndFortune : WatcherCard
{
	public override CardPoolModel Pool => ModelDb.CardPool<ColorlessCardPool>();

	public override string PortraitPath => "res://images/packed/card_portraits/watcher/fame_and_fortune.png";

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 25m) };

	public WatcherWishFameAndFortune()
		: base(-2, CardType.Skill, CardRarity.Token, TargetType.None)
	{
	}

	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return Task.CompletedTask;
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(5m);
	}
}
public sealed class WatcherDrawTalismanRandomDeck : WatcherCard
{
	public override CardPoolModel Pool => ModelDb.CardPool<ColorlessCardPool>();

	public override string PortraitPath => "res://images/packed/card_portraits/watcher/draw_talisman.png";

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 1m) };

	public WatcherDrawTalismanRandomDeck()
		: base(-2, CardType.Skill, CardRarity.Token, TargetType.None, shouldShowInCardLibrary: false)
	{
	}

	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return Task.CompletedTask;
	}
}
public sealed class WatcherDrawTalismanDirectedHand : WatcherCard
{
	public override CardPoolModel Pool => ModelDb.CardPool<ColorlessCardPool>();

	public override string PortraitPath => "res://images/packed/card_portraits/watcher/draw_talisman.png";

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 1m) };

	public WatcherDrawTalismanDirectedHand()
		: base(-2, CardType.Skill, CardRarity.Token, TargetType.None, shouldShowInCardLibrary: false)
	{
	}

	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return Task.CompletedTask;
	}
}
public sealed class WatcherExpunger : WatcherCard
{
	public override CardPoolModel Pool => ModelDb.CardPool<TokenCardPool>();

	public int HitCount
	{
		get
		{
			return base.DynamicVars.Repeat.IntValue;
		}
		set
		{
			AssertMutable();
			base.DynamicVars.Repeat.BaseValue = value;
		}
	}

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[2]
	{
		new DamageVar(9m, ValueProp.Move),
		new RepeatVar(1)
	};

	public WatcherExpunger()
		: base(1, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).WithHitCount(base.DynamicVars.Repeat.IntValue).FromCard((CardModel)this, cardPlay)
			.Targeting(cardPlay.Target)
			.BeforeDamage(delegate
			{
				WatcherAttackVfxHelper.PlayExpunge(cardPlay.Target);
				return Task.CompletedTask;
			})
			.Execute(choiceContext);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(6m);
	}
}
public sealed class WatcherSmite : WatcherCard
{
	public override CardPoolModel Pool => ModelDb.CardPool<TokenCardPool>();

	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[2]
	{
		CardKeyword.Retain,
		CardKeyword.Exhaust
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(12m, ValueProp.Move) };

	public WatcherSmite()
		: base(1, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(choiceContext);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(4m);
	}
}
public sealed class WatcherInsight : WatcherCard
{
	public override CardPoolModel Pool => ModelDb.CardPool<TokenCardPool>();

	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[2]
	{
		CardKeyword.Retain,
		CardKeyword.Exhaust
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar("MagicNumber", 2) };

	public override bool CanBeGeneratedInCombat => false;

	public WatcherInsight()
		: base(0, CardType.Skill, CardRarity.Token, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CardPileCmd.Draw(choiceContext, base.DynamicVars["MagicNumber"].BaseValue, base.Owner);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
internal static class WatcherDerivativeTokens
{
	internal static readonly Type[] Types = new Type[8]
	{
		typeof(WatcherMiracle),
		typeof(WatcherInsight),
		typeof(WatcherSafety),
		typeof(WatcherSmite),
		typeof(WatcherThroughViolence),
		typeof(WatcherBeta),
		typeof(WatcherOmega),
		typeof(WatcherExpunger)
	};
}
internal static class WatcherSimpleBHelper
{
	public static async Task<IEnumerable<CardModel>> TakeFromDiscard(PlayerChoiceContext context, Player owner, int count)
	{
		List<CardModel> list = PileType.Discard.GetPile(owner).Cards.ToList();
		if (list.Count == 0 || count <= 0)
		{
			return System.Array.Empty<CardModel>();
		}
		int num = Math.Min(count, list.Count);
		CardSelectorPrefs prefs = WatcherCombatHelper.SetCancelable(new CardSelectorPrefs(new LocString("card_selection", "TO_UPGRADE"), num, num), value: false);
		return await CardSelectCmd.FromSimpleGrid(context, list, owner, prefs);
	}
}
public sealed class EnergyDownPower : PowerModel
{
	public override PowerType Type => PowerType.Debuff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override async Task AfterEnergyReset(Player player)
	{
		if (player == base.Owner.Player)
		{
			await PlayerCmd.LoseEnergy(base.Amount, player);
		}
	}
}
public sealed class SimmeringFuryPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player == base.Owner.Player)
		{
			Flash();
			await WatcherCombatHelper.EnterWrath(player, null);
			await CardPileCmd.Draw(choiceContext, base.Amount, player);
			await PowerCmd.Remove(this);
		}
	}
}
public sealed class CollectPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		CombatState combatState = WatcherCreatureCompat.GetCombatState(base.Owner);
		if (player == base.Owner.Player && combatState != null)
		{
			Flash();
			WatcherMiracle card = combatState.CreateCard<WatcherMiracle>(player);
			CardCmd.Upgrade(card);
			await WatcherCardPileCompat.AddGeneratedCardToCombat(card, PileType.Hand, addedByPlayer: true);
			await WatcherPowerCmdCompat.ModifyAmount(this, -1m, null, null, silent: true);
			if (base.Amount <= 0)
			{
				await PowerCmd.Remove(this);
			}
		}
	}
}
public sealed class BattleHymnPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player == base.Owner.Player)
		{
			Flash();
			for (int i = 0; i < base.Amount; i++)
			{
				await WatcherCardPileCompat.AddGeneratedCardToCombat(WatcherCreatureCompat.GetCombatState(player.Creature).CreateCard<WatcherSmite>(player), PileType.Hand, addedByPlayer: true);
			}
		}
	}
}
public sealed class WatcherBattleHymn : WatcherCard
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		HoverTipFactory.FromCard<WatcherSmite>(),
		HoverTipFactory.FromPower<BattleHymnPower>(null)
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 1m) };

	public WatcherBattleHymn()
		: base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.TriggerAnim(base.Owner.Creature, "Cast", base.Owner.Character.CastAnimDelay);
		await WatcherPowerCmdCompat.Apply<BattleHymnPower>(base.Owner.Creature, base.DynamicVars["MagicNumber"].BaseValue, base.Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		AddKeyword(CardKeyword.Innate);
	}
}
public sealed class WatcherCarveReality : WatcherCard
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromCard<WatcherSmite>() };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(6m, ValueProp.Move) };

	public WatcherCarveReality()
		: base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(choiceContext);
		await WatcherCardPileCompat.AddGeneratedCardToCombat(base.CombatState.CreateCard<WatcherSmite>(base.Owner), PileType.Hand, addedByPlayer: true);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(4m);
	}
}
public sealed class WatcherCollect : WatcherCard
{
	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

	protected override bool HasEnergyCostX => true;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromCard<WatcherMiracle>(base.IsUpgraded) };

	public WatcherCollect()
		: base(-1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		int num = ResolveEnergyXValue();
		if (base.IsUpgraded)
		{
			num++;
		}
		if (num > 0)
		{
			await WatcherPowerCmdCompat.Apply<CollectPower>(base.Owner.Creature, num, base.Owner.Creature, this);
		}
	}

	protected override void OnUpgrade()
	{
	}
}
public sealed class WatcherConclude : WatcherCard
{
	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(12m, ValueProp.Move) };

	public WatcherConclude()
		: base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AllEnemies)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(base.CombatState, "CombatState");
		WatcherAttackVfxHelper.PlayCleave();
		await WatcherAttackVfxHelper.WaitSeconds(0.1);
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).TargetingAllOpponentsCompat(base.CombatState)
			.Execute(choiceContext);
		if (cardPlay.IsLastInSeries)
		{
			await WatcherCombatHelper.EndTurnSafely(base.Owner);
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(4m);
	}
}
public sealed class WatcherDeceiveReality : WatcherCard
{
	public override bool GainsBlock => true;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromCard<WatcherSafety>() };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(4m, ValueProp.Move) };

	public WatcherDeceiveReality()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
		await WatcherCardPileCompat.AddGeneratedCardToCombat(base.CombatState.CreateCard<WatcherSafety>(base.Owner), PileType.Hand, addedByPlayer: true);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(3m);
	}
}
public sealed class WatcherEmptyMind : WatcherCard
{
	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar("MagicNumber", 2) };

	public WatcherEmptyMind()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CardPileCmd.Draw(choiceContext, base.DynamicVars["MagicNumber"].BaseValue, base.Owner);
		if (WatcherAttackVfxHelper.IsInStance(base.Owner.Creature))
		{
			WatcherAttackVfxHelper.PlayEmptyStance(base.Owner.Creature);
			await WatcherAttackVfxHelper.WaitSeconds(0.1);
		}
		await WatcherCombatHelper.ExitStance(base.Owner);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherFasting2 : WatcherCard
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		HoverTipFactory.FromPower<StrengthPower>(null),
		HoverTipFactory.FromPower<DexterityPower>(null)
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 3m) };

	public WatcherFasting2()
		: base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherPowerCmdCompat.Apply<StrengthPower>(base.Owner.Creature, base.DynamicVars["MagicNumber"].BaseValue, base.Owner.Creature, this);
		await WatcherPowerCmdCompat.Apply<DexterityPower>(base.Owner.Creature, base.DynamicVars["MagicNumber"].BaseValue, base.Owner.Creature, this);
		await WatcherPowerCmdCompat.Apply<EnergyDownPower>(base.Owner.Creature, 1m, base.Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherFearNoEvil : WatcherCard
{
	protected override bool ShouldGlowGoldInternal => base.CombatState?.HittableEnemies.Any((Creature enemy) => WatcherSimpleAHelper.IsTargetAttacking(enemy)) ?? false;

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(8m, ValueProp.Move) };

	public WatcherFearNoEvil()
		: base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		bool targetWasAttacking = WatcherSimpleAHelper.IsTargetAttacking(cardPlay.Target);
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(choiceContext);
		if (targetWasAttacking)
		{
			await WatcherCombatHelper.EnterCalm(base.Owner, this);
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(3m);
	}
}
public sealed class WatcherForeignInfluence : WatcherCard
{
	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

	public WatcherForeignInfluence()
		: base(0, CardType.Skill, CardRarity.Uncommon, TargetType.None)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		List<CardPoolModel> list = base.Owner.UnlockState.CharacterCardPools.ToList();
		if (list.Count > 1)
		{
			list.Remove(base.Owner.Character.CardPool);
		}
		IEnumerable<CardModel> cards = from card in list.SelectMany((CardPoolModel pool) => pool.GetUnlockedCards(base.Owner.UnlockState, base.Owner.RunState.CardMultiplayerConstraint))
			where card.Type == CardType.Attack && card.Rarity != CardRarity.Token
			select card;
		List<CardModel> options = CardFactory.GetDistinctForCombat(base.Owner, cards, 3, base.Owner.RunState.Rng.CombatCardGeneration).ToList();
		CardModel cardModel = await WatcherCombatHelper.ChooseOne(choiceContext, base.Owner, options, new LocString("cards", "WATCHER_FOREIGN_INFLUENCE.selectionScreenPrompt"), cancelable: true);
		if (cardModel != null)
		{
			if (base.IsUpgraded)
			{
				cardModel.SetToFreeThisTurn();
			}
			await WatcherCardPileCompat.AddGeneratedCardToCombat(cardModel, PileType.Hand, addedByPlayer: true);
		}
	}
}
public class WatcherForesight : WatcherCard
{
	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 3m) };

	public WatcherForesight()
		: base(1, CardType.Power, CardRarity.Uncommon, TargetType.None)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherPowerCmdCompat.Apply<ForesightPower>(base.Owner.Creature, base.DynamicVars["MagicNumber"].BaseValue, base.Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherIndignation : WatcherCard
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[3]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Wrath>(null),
		HoverTipFactory.FromPower<VulnerablePower>(null)
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 3m) };

	public WatcherIndignation()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (WatcherCombatHelper.IsInStance<Wrath>(base.Owner.Creature))
		{
			await WatcherPowerCmdCompat.Apply<VulnerablePower>(base.CombatState.HittableEnemies, base.DynamicVars["MagicNumber"].BaseValue, base.Owner.Creature, this);
		}
		else
		{
			await WatcherCombatHelper.EnterWrath(base.Owner, this);
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(2m);
	}
}
public sealed class WatcherInnerPeace : WatcherCard
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Calm>(null)
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar("MagicNumber", 3) };

	public WatcherInnerPeace()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (WatcherCombatHelper.IsInStance<Calm>(base.Owner.Creature))
		{
			await CardPileCmd.Draw(choiceContext, base.DynamicVars["MagicNumber"].BaseValue, base.Owner);
		}
		else
		{
			await WatcherCombatHelper.EnterCalm(base.Owner, this);
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherLikeWater : WatcherCard
{
	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 5m) };

	public WatcherLikeWater()
		: base(1, CardType.Power, CardRarity.Uncommon, TargetType.None)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherPowerCmdCompat.Apply<LikeWaterPower>(base.Owner.Creature, base.DynamicVars["MagicNumber"].BaseValue, base.Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(2m);
	}
}
public sealed class WatcherMeditate : WatcherCard
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Calm>(null)
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar("MagicNumber", 1) };

	public WatcherMeditate()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		foreach (CardModel card in await WatcherSimpleBHelper.TakeFromDiscard(choiceContext, base.Owner, base.DynamicVars["MagicNumber"].IntValue))
		{
			card.GiveSingleTurnRetain();
			await CardPileCmd.Add(card, PileType.Hand);
			CardPile? pile = card.Pile;
			if (pile == null || pile.Type != PileType.Hand)
			{
				WatcherCombatHelper.DeferRetainCard(base.Owner, card);
			}
		}
		await WatcherCombatHelper.EnterCalm(base.Owner, this);
		if (cardPlay.IsLastInSeries)
		{
			await WatcherCombatHelper.EndTurnSafely(base.Owner);
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherMentalFortress : WatcherCard
{
	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 4m) };

	public WatcherMentalFortress()
		: base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherPowerCmdCompat.Apply<MentalFortressPower>(base.Owner.Creature, base.DynamicVars["MagicNumber"].BaseValue, base.Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(2m);
	}
}
public class WatcherNirvana : WatcherCard
{
	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 3m) };

	public WatcherNirvana()
		: base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherPowerCmdCompat.Apply<NirvanaPower>(base.Owner.Creature, base.DynamicVars["MagicNumber"].BaseValue, base.Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherPerseverance : WatcherCard
{
	public override bool GainsBlock => true;

	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Retain };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[2]
	{
		new BlockVar(5m, ValueProp.Move),
		new DynamicVar("MagicNumber", 2m)
	};

	public WatcherPerseverance()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(2m);
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherPray : WatcherCard
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		HoverTipFactory.FromPower<Mantra>(null),
		HoverTipFactory.FromCard<WatcherInsight>()
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 3m) };

	public WatcherPray()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherCombatHelper.GainMantra(base.Owner, base.DynamicVars["MagicNumber"].IntValue, this);
		await WatcherCardPileCompat.AddGeneratedCardToCombat(await WatcherCombatHelper.CreateWatcherCard<WatcherInsight>(base.Owner), PileType.Draw, addedByPlayer: true, CardPilePosition.Random);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherReachHeaven : WatcherCard
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromCard<WatcherThroughViolence>() };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(10m, ValueProp.Move) };

	public WatcherReachHeaven()
		: base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(choiceContext);
		await WatcherCardPileCompat.AddGeneratedCardToCombat(await WatcherCombatHelper.CreateWatcherCard<WatcherThroughViolence>(base.Owner), PileType.Draw, addedByPlayer: true, CardPilePosition.Random);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(5m);
	}
}
public sealed class WatcherRushdown : WatcherCard
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[3]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Wrath>(null),
		HoverTipFactory.FromPower<RushdownPower>(null)
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar("MagicNumber", 2) };

	public WatcherRushdown()
		: base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherPowerCmdCompat.Apply<RushdownPower>(base.Owner.Creature, base.DynamicVars["MagicNumber"].BaseValue, base.Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		base.EnergyCost.UpgradeBy(-1);
	}
}
public sealed class WatcherSanctity : WatcherCard
{
	public override bool GainsBlock => true;

	protected override bool ShouldGlowGoldInternal => WatcherSimpleAHelper.GetPreviousPlayedCardType(this) == CardType.Skill;

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[2]
	{
		new BlockVar(6m, ValueProp.Move),
		new CardsVar("MagicNumber", 2)
	};

	public WatcherSanctity()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
		if (WatcherSimpleAHelper.GetPreviousPlayedCardType(this) == CardType.Skill)
		{
			await CardPileCmd.Draw(choiceContext, base.DynamicVars["MagicNumber"].BaseValue, base.Owner);
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(3m);
	}
}
public sealed class WatcherSandsOfTime : WatcherCard
{
	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Retain };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(20m, ValueProp.Move) };

	public WatcherSandsOfTime()
		: base(4, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(choiceContext);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(6m);
	}
}
public sealed class WatcherSignatureMove : WatcherCard
{
	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(30m, ValueProp.Move) };

	protected override bool IsPlayable
	{
		get
		{
			if (base.Owner == null)
			{
				return true;
			}
			return PileType.Hand.GetPile(base.Owner).Cards.Count((CardModel c) => c.Type == CardType.Attack) <= 1;
		}
	}

	protected override bool ShouldGlowGoldInternal
	{
		get
		{
			if (base.Owner == null)
			{
				return false;
			}
			return PileType.Hand.GetPile(base.Owner).Cards.Count((CardModel c) => c.Type == CardType.Attack) <= 1;
		}
	}

	public WatcherSignatureMove()
		: base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		Creature caster = base.Owner.Creature;
		bool swing = WatcherAttackVfxHelper.HasSignatureSwing(caster);
		WatcherAttackVfxHelper.SetStaffEyeAnimation(caster, "Wrath");
		try
		{
			if (swing)
			{
				WatcherAttackVfxHelper.SetSpineTimeScale(caster, 1f);
				WatcherAttackVfxHelper.TriggerSignatureSwing(caster);
			}
			await WatcherAttackVfxHelper.WaitRealSeconds(swing ? 0.17 : 0.03);
			WatcherAttackVfxHelper.PlaySignatureWave(caster, cardPlay.Target);
			await WatcherAttackVfxHelper.WaitRealSeconds(0.28999999046325686);
			AttackCommand attackCommand = DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target);
			attackCommand = (swing ? attackCommand.WithNoAttackerAnim() : attackCommand.WithAttackerAnim("Attack", 0.1f));
			await attackCommand.Execute(choiceContext);
		}
		finally
		{
			if (swing)
			{
				WatcherAttackVfxHelper.SetSpineTimeScale(caster, 1f);
			}
		}
		WatcherAttackVfxHelper.PlaySignatureImpact(cardPlay.Target);
		WatcherAttackVfxHelper.FireAndForget(RestStaffEye(caster));
	}

	private static async Task RestStaffEye(Creature caster)
	{
		await WatcherAttackVfxHelper.WaitRealSeconds(0.6);
		WatcherAttackVfxHelper.SetStaffEyeAnimation(caster, "None");
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(10m);
	}
}
public sealed class WatcherSimmeringFury : WatcherCard
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Wrath>(null)
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new CardsVar("MagicNumber", 2) };

	public WatcherSimmeringFury()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherPowerCmdCompat.Apply<SimmeringFuryPower>(base.Owner.Creature, base.DynamicVars["MagicNumber"].BaseValue, base.Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherStudy : WatcherCard
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		HoverTipFactory.FromCard<WatcherInsight>(),
		HoverTipFactory.FromPower<StudyPower>(null)
	};

	public WatcherStudy()
		: base(2, CardType.Power, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherPowerCmdCompat.Apply<StudyPower>(base.Owner.Creature, 1m, base.Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		base.EnergyCost.UpgradeBy(-1);
	}
}
public class WatcherSwivel : WatcherCard
{
	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new BlockVar(8m, ValueProp.Move) };

	public WatcherSwivel()
		: base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await CreatureCmd.GainBlock(base.Owner.Creature, base.DynamicVars.Block, cardPlay);
		await WatcherPowerCmdCompat.Apply<FreeAttackPower>(base.Owner.Creature, 1m, base.Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Block.UpgradeValueBy(3m);
	}
}
public sealed class WatcherTalkToTheHand : WatcherCard
{
	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[2]
	{
		new DamageVar(5m, ValueProp.Move),
		new DynamicVar("MagicNumber", 2m)
	};

	public WatcherTalkToTheHand()
		: base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(choiceContext);
		await WatcherPowerCmdCompat.Apply<BlockReturnPower>(cardPlay.Target, base.DynamicVars["MagicNumber"].BaseValue, base.Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(2m);
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherTantrum : WatcherCard
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Wrath>(null)
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[2]
	{
		new DamageVar(3m, ValueProp.Move),
		new DynamicVar("MagicNumber", 3m)
	};

	public WatcherTantrum()
		: base(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		int intValue = base.DynamicVars["MagicNumber"].IntValue;
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).WithHitCount(intValue).FromCard((CardModel)this, cardPlay)
			.Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(choiceContext);
		await WatcherCombatHelper.EnterWrath(base.Owner, this);
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (cardPlay.Card == this)
		{
			CardPile? pile = base.Pile;
			if (pile != null && pile.Type == PileType.Play && !base.IsDupe)
			{
				await CardPileCmd.Add(this, PileType.Draw, CardPilePosition.Random);
			}
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherWallop : WatcherCard
{
	public override bool GainsBlock => true;

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(9m, ValueProp.Move) };

	public WatcherWallop()
		: base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		int hpBefore = cardPlay.Target.CurrentHp;
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_heavy_blunt")
			.Execute(choiceContext);
		int num = hpBefore - cardPlay.Target.CurrentHp;
		if (num > 0)
		{
			WatcherAttackVfxHelper.PlayWallop(num, cardPlay.Target);
			await CreatureCmd.GainBlock(base.Owner.Creature, num, ValueProp.Unpowered | ValueProp.Move, cardPlay);
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(3m);
	}
}
public sealed class WatcherWaveOfTheHand : WatcherCard
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		HoverTipFactory.FromPower<WeakPower>(null),
		HoverTipFactory.FromPower<WaveOfTheHandPower>(null)
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 1m) };

	public WatcherWaveOfTheHand()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.None)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherPowerCmdCompat.Apply<WaveOfTheHandPower>(base.Owner.Creature, base.DynamicVars["MagicNumber"].BaseValue, base.Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherWeave : WatcherCard
{
	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DamageVar(4m, ValueProp.Move) };

	public WatcherWeave()
		: base(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_slash")
			.Execute(choiceContext);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(2m);
	}
}
public sealed class WatcherWheelKick : WatcherCard
{
	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[2]
	{
		new DamageVar(15m, ValueProp.Move),
		new CardsVar("MagicNumber", 2)
	};

	public WatcherWheelKick()
		: base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(choiceContext);
		await CardPileCmd.Draw(choiceContext, base.DynamicVars["MagicNumber"].BaseValue, base.Owner);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(5m);
	}
}
public sealed class WatcherWindmillStrike : WatcherCard
{
	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Retain };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[2]
	{
		new DamageVar(7m, ValueProp.Move),
		new DynamicVar("MagicNumber", 4m)
	};

	public WatcherWindmillStrike()
		: base(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		ArgumentNullException.ThrowIfNull(cardPlay.Target, "cardPlay.Target");
		await DamageCmd.Attack(base.DynamicVars.Damage.BaseValue).FromCard((CardModel)this, cardPlay).Targeting(cardPlay.Target)
			.WithHitFx("vfx/vfx_attack_blunt")
			.Execute(choiceContext);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Damage.UpgradeValueBy(3m);
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherWorship : WatcherCard
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Mantra>(null)
	};

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 5m) };

	public WatcherWorship()
		: base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherCombatHelper.GainMantra(base.Owner, base.DynamicVars["MagicNumber"].IntValue, this);
	}

	protected override void OnUpgrade()
	{
		AddKeyword(CardKeyword.Retain);
	}
}
public sealed class WatcherWreathOfFlame : WatcherCard
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromPower<VigorPower>(null) };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 5m) };

	public WatcherWreathOfFlame()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		await WatcherPowerCmdCompat.Apply<VigorPower>(base.Owner.Creature, base.DynamicVars["MagicNumber"].BaseValue, base.Owner.Creature, this);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(3m);
	}
}
public sealed class WatcherRevelation_P : WatcherCard, IOnScryDiscarded
{
	public override string PortraitPath => "res://images/packed/card_portraits/watcher/revelation.png";

	public override string BetaPortraitPath => "res://images/packed/card_portraits/watcher/beta/revelation.png";

	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Unplayable };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new EnergyVar(1) };

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { base.EnergyHoverTip };

	public WatcherRevelation_P()
		: base(-1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return Task.CompletedTask;
	}

	public async Task OnScryDiscarded(PlayerChoiceContext choiceContext, Player owner)
	{
		await PlayerCmd.GainEnergy(base.DynamicVars.Energy.BaseValue, owner);
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars.Energy.UpgradeValueBy(1m);
	}
}
public sealed class WatcherHymnV2 : WatcherCard, IOnScryDiscarded
{
	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Unplayable };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[2]
	{
		new DynamicVar("MagicNumber", 2m),
		new PowerVar<EnlightenFatePower>(2m)
	};

	public override string PortraitPath => "res://images/packed/card_portraits/watcher/hymn.png";

	public override string BetaPortraitPath => "res://images/packed/card_portraits/watcher/beta/hymn.png";

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[3]
	{
		WatcherHoverTips.Finality,
		HoverTipFactory.FromPower<KnowFatePower>(null),
		HoverTipFactory.FromPower<EnlightenFatePower>(null)
	};

	public WatcherHymnV2()
		: base(-1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
	{
	}

	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return Task.CompletedTask;
	}

	public async Task OnScryDiscarded(PlayerChoiceContext choiceContext, Player owner)
	{
		int cap = base.DynamicVars["MagicNumber"].IntValue;
		if (await WatcherCombatHelper.ConsumeKnowFate(owner, cap, owner.Creature, this) >= cap)
		{
			await WatcherPowerCmdCompat.Apply<EnlightenFatePower>(owner.Creature, base.DynamicVars[typeof(EnlightenFatePower).Name].BaseValue, owner.Creature, this);
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars[typeof(EnlightenFatePower).Name].UpgradeValueBy(1m);
	}
}
public sealed class WatcherColdObservation : WatcherCard
{
	public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Retain };

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("MagicNumber", 1m) };

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromPower<ColdObservationPower>(null) };

	public WatcherColdObservation()
		: base(2, CardType.Skill, CardRarity.Uncommon, TargetType.AllAllies)
	{
	}

	protected override void AddExtraArgsToDescription(LocString description)
	{
		string text;
		switch (LocManager.Instance?.Language ?? "eng")
		{
		case "kor":
			text = "모든 플레이어의";
			break;
		case "jpn":
			text = "全プレイヤーの";
			break;
		case "jpn2":
			text = "全プレイヤーの";
			break;
		case "zhs":
		case "zht":
			text = "所有玩家的";
			break;
		default:
			text = "All players'";
			break;
		}
		string variable = text;
		description.Add("Subject", variable);
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		IEnumerable<Creature> enumerable = from c in base.CombatState.GetTeammatesOf(base.Owner.Creature)
			where c != null && c.IsAlive && c.IsPlayer
			select c;
		foreach (Creature item in enumerable)
		{
			await WatcherPowerCmdCompat.Apply<ColdObservationPower>(item, base.DynamicVars["MagicNumber"].BaseValue, base.Owner.Creature, this);
		}
	}

	protected override void OnUpgrade()
	{
		base.DynamicVars["MagicNumber"].UpgradeValueBy(1m);
	}
}
public sealed class WatcherMockery : WatcherCard
{
	internal bool IsGeneratedCopy { get; set; }

	public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[3]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Wrath>(null),
		HoverTipFactory.FromCard<WatcherPersuasion>()
	};

	public WatcherMockery()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllAllies)
	{
	}

	protected override void AddExtraArgsToDescription(LocString description)
	{
		description.Add("ShowGenerate", !IsGeneratedCopy);
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		IEnumerable<Creature> enumerable = from c in base.CombatState.GetTeammatesOf(base.Owner.Creature)
			where c != null && c.IsAlive && c.IsPlayer && c != base.Owner.Creature
			select c;
		foreach (Creature item in enumerable)
		{
			if (item.Player != null)
			{
				await WatcherCombatHelper.EnterWrath(item.Player, this);
			}
		}
		if (!IsGeneratedCopy)
		{
			WatcherPersuasion watcherPersuasion = base.CombatState.CreateCard<WatcherPersuasion>(base.Owner);
			watcherPersuasion.IsGeneratedCopy = true;
			watcherPersuasion.EnergyCost.SetCustomBaseCost(0);
			await WatcherCardPileCompat.AddGeneratedCardToCombat(watcherPersuasion, PileType.Hand, addedByPlayer: true);
		}
	}

	protected override void OnUpgrade()
	{
		AddKeyword(CardKeyword.Retain);
	}
}
public sealed class WatcherPersuasion : WatcherCard
{
	internal bool IsGeneratedCopy { get; set; }

	public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;

	public override IEnumerable<CardKeyword> CanonicalKeywords => new CardKeyword[] { CardKeyword.Exhaust };

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[3]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Calm>(null),
		HoverTipFactory.FromCard<WatcherMockery>()
	};

	public WatcherPersuasion()
		: base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AllAllies)
	{
	}

	protected override void AddExtraArgsToDescription(LocString description)
	{
		description.Add("ShowGenerate", !IsGeneratedCopy);
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		IEnumerable<Creature> enumerable = from c in base.CombatState.GetTeammatesOf(base.Owner.Creature)
			where c != null && c.IsAlive && c.IsPlayer && c != base.Owner.Creature
			select c;
		foreach (Creature item in enumerable)
		{
			if (item.Player != null)
			{
				await WatcherCombatHelper.EnterCalm(item.Player, this);
			}
		}
		if (!IsGeneratedCopy)
		{
			WatcherMockery watcherMockery = base.CombatState.CreateCard<WatcherMockery>(base.Owner);
			watcherMockery.IsGeneratedCopy = true;
			watcherMockery.EnergyCost.SetCustomBaseCost(0);
			await WatcherCardPileCompat.AddGeneratedCardToCombat(watcherMockery, PileType.Hand, addedByPlayer: true);
		}
	}

	protected override void OnUpgrade()
	{
		AddKeyword(CardKeyword.Retain);
	}
}
internal static class WatcherAttackCommandCompat
{
	[CompilerGenerated]

	private static readonly MethodInfo? TargetingAllOpponentsMethod = FindTargetingMethod("TargetingAllOpponents");

	private static readonly MethodInfo? TargetingRandomOpponentsMethod = FindTargetingMethod("TargetingRandomOpponents");

	private static readonly FieldInfo? ResultsField = typeof(AttackCommand).GetField("_results", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly PropertyInfo? ResultsProperty = typeof(AttackCommand).GetProperty("Results", BindingFlags.Instance | BindingFlags.Public);

	private static readonly MethodInfo? FromCardWithCardPlayMethod = FindFromCardMethod(typeof(CardModel), typeof(CardPlay));

	private static readonly MethodInfo? FromCardMethod = FindFromCardMethod(typeof(CardModel));

	public static AttackCommand FromCard(this AttackCommand command, CardModel card, CardPlay? cardPlay)
	{
		ArgumentNullException.ThrowIfNull(command, "command");
		ArgumentNullException.ThrowIfNull(card, "card");
		if (FromCardWithCardPlayMethod != null)
		{
			return InvokeFromCard(command, FromCardWithCardPlayMethod, card, cardPlay);
		}
		if (FromCardMethod != null)
		{
			return InvokeFromCard(command, FromCardMethod, card);
		}
		throw new MissingMethodException(typeof(AttackCommand).FullName, "FromCard");
	}

	public static AttackCommand TargetingAllOpponentsCompat(this AttackCommand command, object? combatState)
	{
		return InvokeTargeting(command, TargetingAllOpponentsMethod, combatState) ?? command;
	}

	public static AttackCommand TargetingRandomOpponentsCompat(this AttackCommand command, object? combatState, bool allowDuplicates = true)
	{
		return InvokeTargeting(command, TargetingRandomOpponentsMethod, combatState, allowDuplicates) ?? command;
	}

	public static IEnumerable<DamageResult> GetDamageResults(AttackCommand command)
	{
		ArgumentNullException.ThrowIfNull(command, "command");
		object obj = TryGetValue(ResultsField, command);
		if (obj == null)
		{
			obj = TryGetValue(ResultsProperty, command);
		}
		return FlattenDamageResults(obj);
	}

	private static object? TryGetValue(FieldInfo? field, AttackCommand command)
	{
		if (field == null)
		{
			return null;
		}
		try
		{
			return field.GetValue(command);
		}
		catch (Exception ex) when (ex is ArgumentException || ex is FieldAccessException || ex is InvalidOperationException || ex is TargetException)
		{
			return null;
		}
	}

	private static object? TryGetValue(PropertyInfo? property, AttackCommand command)
	{
		if (property == null)
		{
			return null;
		}
		try
		{
			return property.GetValue(command);
		}
		catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is MethodAccessException || ex is MissingMethodException || ex is TargetException || ex is TargetInvocationException)
		{
			return null;
		}
	}

	private static IEnumerable<DamageResult> FlattenDamageResults(object? value)
	{
		if (value == null || value is string)
		{
			yield break;
		}
		if (value is DamageResult damageResult)
		{
			yield return damageResult;
			yield break;
		}
		if (value is IEnumerable enumerable)
		{
			foreach (object item in enumerable)
			{
				foreach (DamageResult nested in FlattenDamageResults(item))
				{
					yield return nested;
				}
			}
		}
	}

	private static MethodInfo? FindFromCardMethod(params Type[] parameterTypes)
	{
		return typeof(AttackCommand).GetMethod("FromCard", BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null);
	}

	private static AttackCommand InvokeFromCard(AttackCommand command, MethodInfo method, params object?[] args)
	{
		try
		{
			return (AttackCommand)method.Invoke(command, args);
		}
		catch (TargetInvocationException ex) when (ex.InnerException != null)
		{
			ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
			throw;
		}
	}

	private static MethodInfo? FindTargetingMethod(string methodName)
	{
		return typeof(AttackCommand).GetMethods(BindingFlags.Instance | BindingFlags.Public).FirstOrDefault(delegate(MethodInfo method)
		{
			ParameterInfo[] parameters = method.GetParameters();
			bool flag = method.Name == methodName && method.ReturnType == typeof(AttackCommand);
			if (flag)
			{
				int num = parameters.Length;
				bool flag2 = (uint)(num - 1) <= 1u;
				flag = flag2;
			}
			return flag && WatcherHookCompat.IsCombatStateParameter(parameters[0].ParameterType) && (parameters.Length == 1 || parameters[1].ParameterType == typeof(bool));
		});
	}

	private static AttackCommand? InvokeTargeting(AttackCommand command, MethodInfo? method, object? combatState, bool allowDuplicates = true)
	{
		if (method == null || combatState == null)
		{
			return null;
		}
		ParameterInfo[] parameters = method.GetParameters();
		object obj = WatcherHookCompat.ToCompatibleCombatState(combatState, parameters[0].ParameterType);
		if (obj == null)
		{
			return null;
		}
		object[] parameters2 = ((parameters.Length != 1) ? new object[2] { obj, allowDuplicates } : new object[1] { obj });
		try
		{
			return method.Invoke(command, parameters2) as AttackCommand;
		}
		catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is MissingMethodException || ex is TargetInvocationException)
		{
			return null;
		}
	}
}
internal static class WatcherAttackVfxHelper
{
	private static class StsAssets
	{
		private readonly record struct AtlasRegion(string Page, int X, int Y, int W, int H, int OrigW, int OrigH, int OffsetX, int OffsetY);

		private const string VfxAtlasPath = "res://images/vfx/sts1/vfx.atlas";

		private const string Vfx1Path = "res://images/vfx/sts1/vfx.png";

		private const string Vfx2Path = "res://images/vfx/sts1/vfx2.png";

		private const string SlashPath = "res://images/vfx/combat/slash_1.png";

		private static readonly System.Collections.Generic.Dictionary<string, Texture2D?> Cache = new System.Collections.Generic.Dictionary<string, Texture2D>();

		private static readonly HashSet<string> MissingTextureWarnings = new HashSet<string>();

		private static readonly AtlasRegion[] EyeRegions = new AtlasRegion[7]
		{
			new AtlasRegion("vfx.png", 1895, 1214, 62, 25, 62, 25, 0, 0),
			new AtlasRegion("vfx.png", 1427, 261, 64, 26, 64, 26, 0, 0),
			new AtlasRegion("vfx.png", 1708, 117, 64, 32, 64, 32, 0, 0),
			new AtlasRegion("vfx.png", 1905, 242, 64, 38, 64, 38, 0, 0),
			new AtlasRegion("vfx.png", 1839, 245, 64, 43, 64, 43, 0, 0),
			new AtlasRegion("vfx.png", 1838, 74, 64, 46, 64, 46, 0, 0),
			new AtlasRegion("vfx.png", 1894, 192, 64, 48, 64, 48, 0, 0)
		};

		private static System.Collections.Generic.Dictionary<string, AtlasRegion>? _regions;

		internal static Texture2D? Slash => LoadTexture("res://images/vfx/combat/slash_1.png");

		internal static Texture2D? EyeFrame(int index)
		{
			if (index < 0 || index >= EyeRegions.Length)
			{
				index = 0;
			}
			return LoadTexture($"res://images/vfx/sts1_eye/eye{index}.png") ?? Region(EyeRegions[index], $"combat/stance/eye{index}#direct");
		}

		internal static Texture2D? Region(string name, bool preserveOriginalSize = false)
		{
			string text = (preserveOriginalSize ? (name + "#orig") : name);
			if (Cache.TryGetValue(text, out Texture2D value))
			{
				return value;
			}
			EnsureRegions();
			if (_regions == null || !_regions.TryGetValue(name, out var value2))
			{
				Cache[text] = null;
				return null;
			}
			return Region(value2, text, preserveOriginalSize);
		}

		private static Texture2D? Region(AtlasRegion r, string cacheKey, bool preserveOriginalSize = false)
		{
			if (Cache.TryGetValue(cacheKey, out Texture2D value))
			{
				return value;
			}
			Texture2D texture2D = LoadTexture((r.Page == "vfx2.png") ? "res://images/vfx/sts1/vfx2.png" : "res://images/vfx/sts1/vfx.png");
			if (texture2D == null)
			{
				return null;
			}
			AtlasTexture atlasTexture = new AtlasTexture
			{
				Atlas = texture2D,
				Region = new Rect2(r.X, r.Y, r.W, r.H)
			};
			if (preserveOriginalSize && r.OrigW > 0 && r.OrigH > 0)
			{
				int num = Math.Max(0, r.OrigH - r.OffsetY - r.H);
				atlasTexture.Margin = new Rect2(r.OffsetX, num, Math.Max(0, r.OrigW - r.W), Math.Max(0, r.OrigH - r.H));
			}
			Cache[cacheKey] = atlasTexture;
			return atlasTexture;
		}

		private static Texture2D? LoadTexture(string path)
		{
			if (Cache.TryGetValue(path, out Texture2D value))
			{
				return value;
			}
			Texture2D texture2D = WatcherTextureHelper.LoadTexture(path);
			if (texture2D == null)
			{
				if (MissingTextureWarnings.Add(path))
				{
					GD.PushWarning("Watcher VFX texture failed to load: " + path);
				}
				return null;
			}
			Cache[path] = texture2D;
			return texture2D;
		}

		private static void EnsureRegions()
		{
			if (_regions != null)
			{
				return;
			}
			_regions = new System.Collections.Generic.Dictionary<string, AtlasRegion>();
			if (!Godot.FileAccess.FileExists("res://images/vfx/sts1/vfx.atlas"))
			{
				return;
			}
			using Godot.FileAccess fileAccess = Godot.FileAccess.Open("res://images/vfx/sts1/vfx.atlas", Godot.FileAccess.ModeFlags.Read);
			string page = "vfx.png";
			while (!fileAccess.EofReached())
			{
				string text = fileAccess.GetLine().TrimEnd().Trim();
				if (text.Length == 0)
				{
					continue;
				}
				if (text.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
				{
					page = text;
				}
				else
				{
					if (text.Contains(':'))
					{
						continue;
					}
					string key = text;
					bool flag = false;
					int x = 0;
					int y = 0;
					int num = 0;
					int num2 = 0;
					int num3 = 0;
					int num4 = 0;
					int offsetX = 0;
					int offsetY = 0;
					for (int i = 0; i < 8; i++)
					{
						if (fileAccess.EofReached())
						{
							break;
						}
						string text2 = fileAccess.GetLine().Trim();
						if (text2.StartsWith("rotate:"))
						{
							flag = text2.Contains("true", StringComparison.OrdinalIgnoreCase);
						}
						else if (text2.StartsWith("xy:"))
						{
							int[] array = ParseInts(text2);
							if (array.Length >= 2)
							{
								x = array[0];
								y = array[1];
							}
						}
						else if (text2.StartsWith("size:"))
						{
							int[] array2 = ParseInts(text2);
							if (array2.Length >= 2)
							{
								num = array2[0];
								num2 = array2[1];
							}
						}
						else if (text2.StartsWith("orig:"))
						{
							int[] array3 = ParseInts(text2);
							if (array3.Length >= 2)
							{
								num3 = array3[0];
								num4 = array3[1];
							}
						}
						else if (text2.StartsWith("offset:"))
						{
							int[] array4 = ParseInts(text2);
							if (array4.Length >= 2)
							{
								offsetX = array4[0];
								offsetY = array4[1];
							}
						}
						else if (text2.StartsWith("index:"))
						{
							break;
						}
					}
					if (!flag && num > 0 && num2 > 0)
					{
						if (num3 <= 0)
						{
							num3 = num;
						}
						if (num4 <= 0)
						{
							num4 = num2;
						}
						_regions[key] = new AtlasRegion(page, x, y, num, num2, num3, num4, offsetX, offsetY);
					}
				}
			}
		}

		private static int[] ParseInts(string prop)
		{
			int num = prop.IndexOf(':');
			if (num >= 0)
			{
				string text = prop;
				int num2 = num + 1;
				prop = text.Substring(num2, text.Length - num2);
			}
			int result;
			return (from s in prop.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				select int.TryParse(s, out result) ? result : 0).ToArray();
		}
	}

	private interface IVfxAnimator
	{
		bool Advance(float delta);

		void Kill();
	}

	private static class VfxDriver
	{
		private static readonly List<IVfxAnimator> Animators = new List<IVfxAnimator>();

		private static readonly List<IVfxAnimator> Pending = new List<IVfxAnimator>();

		private static bool _hooked;

		private static bool _ticking;

		private static double _lastTime;

		internal static void Add(IVfxAnimator? animator)
		{
			if (animator != null)
			{
				EnsureHooked();
				(_ticking ? Pending : Animators).Add(animator);
			}
		}

		private static void EnsureHooked()
		{
			if (!_hooked && Engine.GetMainLoop() is SceneTree sceneTree)
			{
				sceneTree.ProcessFrame += Tick;
				_hooked = true;
				_lastTime = (double)Time.GetTicksMsec() / 1000.0;
			}
		}

		private static void Tick()
		{
			double num = (double)Time.GetTicksMsec() / 1000.0;
			float num2 = (float)Math.Max(0.0, num - _lastTime);
			_lastTime = num;
			if (num2 > 0.1f)
			{
				num2 = 0.1f;
			}
			num2 *= (float)Engine.TimeScale;
			if (NCombatRoom.Instance == null || NonInteractiveMode.IsActive)
			{
				foreach (IVfxAnimator animator in Animators)
				{
					try
					{
						animator.Kill();
					}
					catch
					{
					}
				}
				Animators.Clear();
				Pending.Clear();
				return;
			}
			_ticking = true;
			for (int num3 = Animators.Count - 1; num3 >= 0; num3--)
			{
				IVfxAnimator vfxAnimator = Animators[num3];
				bool flag;
				try
				{
					flag = vfxAnimator.Advance(num2);
				}
				catch
				{
					flag = false;
				}
				if (!flag)
				{
					try
					{
						vfxAnimator.Kill();
					}
					catch
					{
					}
					Animators.RemoveAt(num3);
				}
			}
			_ticking = false;
			if (Pending.Count > 0)
			{
				Animators.AddRange(Pending);
				Pending.Clear();
			}
		}
	}

	private abstract class VfxAnimator : IVfxAnimator
	{
		protected Node2D? Root;

		protected bool Failed;

		protected bool RootDead
		{
			get
			{
				if (!Failed && Root != null)
				{
					return !GodotObject.IsInstanceValid(Root);
				}
				return true;
			}
		}

		protected bool MountRoot(Vector2 position, float rotationDeg = 0f, bool behind = false)
		{
			Root = new Node2D
			{
				Position = position,
				RotationDegrees = rotationDeg
			};
			if (!TryAddToCombatVfx(Root, behind))
			{
				Root = null;
				Failed = true;
				return false;
			}
			return true;
		}

		public abstract bool Advance(float delta);

		public virtual void Kill()
		{
			if (Root != null && GodotObject.IsInstanceValid(Root))
			{
				Root.QueueFree();
			}
			Root = null;
		}
	}

	private sealed class StsAnimatedSlashEffect : VfxAnimator
	{
		private const float StartingDuration = 0.4f;

		private readonly Vector2 _s;

		private readonly Vector2 _t;

		private readonly float _targetScale;

		private readonly Color _baseColor2;

		private float _duration = 0.4f;

		private float _scaleX = 0.01f;

		private Color _color;

		private readonly Sprite2D? _back;

		private readonly Sprite2D? _front;

		internal StsAnimatedSlashEffect(Vector2 p, Vector2 offset, float rotation, float targetScale, Color color, Color color2)
		{
			_targetScale = targetScale;
			_baseColor2 = color2;
			_color = color;
			_color.A = 0f;
			_t = (_s = p - new Vector2(64f, 64f) - offset / 2f * 1f) + offset / 2f * 1f;
			if (MountRoot(_s, rotation))
			{
				_back = Sprite(StsAssets.Slash, Vector2.Zero, _baseColor2, 0f, additive: true);
				_front = Sprite(StsAssets.Slash, Vector2.Zero, _color, 0f, additive: true);
				if (_back != null)
				{
					Root.AddChild(_back, forceReadableName: false, Node.InternalMode.Disabled);
				}
				if (_front != null)
				{
					Root.AddChild(_front, forceReadableName: false, Node.InternalMode.Disabled);
				}
				if (_back == null && _front == null)
				{
					Failed = true;
				}
			}
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			if (_duration > 0.2f)
			{
				float t = (_duration - 0.2f) / 0.2f;
				_color.A = Exp10In(0.8f, 0f, t);
				_scaleX = Exp10In(_targetScale, 0.1f, t);
				Root.Position = new Vector2(Fade(_t.X, _s.X, t), Fade(_t.Y, _s.Y, t));
			}
			else
			{
				float t2 = _duration / 0.2f;
				_scaleX = Pow2In(0.5f, _targetScale, t2);
				_color.A = Pow5In(0f, 0.8f, t2);
			}
			if (_back != null)
			{
				_back.Modulate = _baseColor2;
				_back.Scale = new Vector2(_scaleX * 0.4f * R(0.95f, 1.05f), _scaleX * 0.7f * R(0.95f, 1.05f));
			}
			if (_front != null)
			{
				_front.Modulate = _color;
				_front.Scale = new Vector2(_scaleX * 0.7f * R(0.95f, 1.05f), _scaleX * R(0.95f, 1.05f));
			}
			_duration -= delta;
			return _duration >= 0f;
		}
	}

	private sealed class StsCleaveEffect : VfxAnimator
	{
		private float _fadeIn = 0.05f;

		private float _fadeOut = 0.4f;

		private float _stall;

		private float _scale = 1.2f;

		private Color _color = new Color(1f, 1f, 1f, 0f);

		private readonly Sprite2D? _sprite;

		internal StsCleaveEffect(bool reversed)
		{
			if (MountRoot(Vector2.Zero))
			{
				Texture2D texture = StsAssets.Region("combat/cleave");
				_sprite = Sprite(texture, Vector2.Zero, _color, R(-5f, 1f));
				if (_sprite == null)
				{
					Failed = true;
					return;
				}
				Root.AddChild(_sprite, forceReadableName: false, Node.InternalMode.Disabled);
				Vector2 size = Root.GetViewportRect().Size;
				Root.Position = new Vector2(size.X * (reversed ? 0.3f : 0.7f), size.Y * 0.55f + 100f);
				_stall = R(0f, 0.2f);
				Root.Scale = Vector2.One * _scale;
			}
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			if (_stall > 0f)
			{
				_stall -= delta;
				return true;
			}
			Root.Position += new Vector2(100f * delta, 0f);
			Root.RotationDegrees += R(-0.5f, 0.5f);
			_scale += 0.005f;
			Root.Scale = Vector2.One * _scale;
			if (_fadeIn > 0f)
			{
				_fadeIn = Math.Max(0f, _fadeIn - delta);
				_color.A = Fade(1f, 0f, _fadeIn / 0.05f);
			}
			else
			{
				_fadeOut = Math.Max(0f, _fadeOut - delta);
				_color.A = Pow2(0f, 1f, _fadeOut / 0.4f);
			}
			if (_sprite != null)
			{
				_sprite.Modulate = _color;
			}
			return _fadeOut > 0f;
		}
	}

	private sealed class StsFlickCoinEffect : VfxAnimator
	{
		private const float FlightSeconds = 0.5f;

		private readonly Vector2 _from;

		private readonly Vector2 _to;

		private readonly float _arcHeight;

		private readonly float _spin;

		private float _time;

		private float _sparkleTimer;

		private Color _color = new Color(1f, 1f, 0f, 0f);

		private readonly Sprite2D? _sprite;

		internal StsFlickCoinEffect(Vector2 s, Vector2 d)
		{
			_from = s;
			_to = d - new Vector2(0f, 100f);
			_spin = ((_to.X > _from.X) ? (-1000f) : 1000f);
			_arcHeight = 260f + Math.Max(0f, _from.Y - _to.Y) * 0.35f;
			if (MountRoot(s))
			{
				_sprite = Sprite(StsAssets.Region("combat/empowerCircle1"), Vector2.Zero, _color, 0f, additive: true);
				if (_sprite == null)
				{
					Failed = true;
				}
				else
				{
					Root.AddChild(_sprite, forceReadableName: false, Node.InternalMode.Disabled);
				}
			}
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			_time += delta;
			float num = Clamp01(_time / 0.5f);
			Vector2 vector = _from.Lerp(_to, num);
			vector.Y -= _arcHeight * 4f * num * (1f - num);
			Root.Position = vector;
			_color.A = Clamp01(num / 0.2f);
			if (_sprite != null)
			{
				_sprite.Modulate = _color;
				_sprite.RotationDegrees += _spin * delta;
			}
			if (num > 0.2f && _sparkleTimer < 0f)
			{
				for (int i = 0; i < RI(2, 5); i++)
				{
					Emit(new StsSimpleSparkleEffect(vector));
				}
				_sparkleTimer = R(0.05f, 0.1f);
			}
			_sparkleTimer -= delta;
			if (num >= 1f)
			{
				Emit(new StsSimpleSparkleEffect(_to + new Vector2(0f, 100f), Colors.Gold));
				return false;
			}
			return true;
		}
	}

	private sealed class StsStarBounceEffect : VfxAnimator
	{
		private const float FloorBelowCentre = 95f;

		private float _duration = R(0.5f, 1f);

		private float _vX = R(-900f, 900f);

		private float _vY = R(-1000f, -500f);

		private readonly float _floor;

		private readonly float _scale = R(0.5f, 2f);

		private Color _color = new Color(R(0.8f, 1f), R(0.6f, 0.8f), R(0f, 0.6f), 0f);

		private readonly Sprite2D? _sprite;

		internal StsStarBounceEffect(Vector2 p)
		{
			_floor = p.Y + 95f + R(-25f, 25f);
			if (MountRoot(p, R(0f, 360f)))
			{
				_sprite = Sprite(StsAssets.Region("combat/tinyStar2") ?? StsAssets.Region("combat/tinyStar"), Vector2.Zero, _color, 0f, additive: true);
				if (_sprite == null)
				{
					Failed = true;
				}
				else
				{
					Root.AddChild(_sprite, forceReadableName: false, Node.InternalMode.Disabled);
				}
			}
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			_vY += 3000f / _scale * delta;
			Root.Position += new Vector2(_vX, _vY) * delta;
			Root.Rotation = new Vector2(_vX, _vY).Angle();
			if (Root.Position.Y > _floor)
			{
				_vY = (0f - _vY) * 0.75f;
				Root.Position = new Vector2(Root.Position.X, _floor - 0.1f);
				_vX *= 1.1f;
			}
			_color.A = ((1f - _duration < 0.1f) ? Fade(0f, 1f, (1f - _duration) * 10f) : Pow2Out(0f, 1f, _duration));
			if (_sprite != null)
			{
				_sprite.Modulate = _color;
				_sprite.Scale = new Vector2(_scale * R(0.8f, 1.2f), _scale * R(0.8f, 1.2f));
			}
			_duration -= delta;
			return _duration >= 0f;
		}
	}

	private sealed class StsEmptyStanceEffect : VfxAnimator
	{
		private readonly Vector2 _p;

		private int _num = 10;

		internal StsEmptyStanceEffect(Vector2 p)
		{
			_p = p;
		}

		public override bool Advance(float delta)
		{
			if (_num == 10)
			{
				Emit(new StsSimpleSparkleEffect(_p, Colors.SkyBlue));
			}
			for (int i = 0; i < 3; i++)
			{
				Emit(new StsEmptyStanceParticleEffect(_p));
			}
			_num--;
			return _num > 0;
		}
	}

	private sealed class StsEmptyStanceParticleEffect : VfxAnimator
	{
		private const float StartingDuration = 0.6f;

		private float _duration = 0.6f;

		private readonly float _rotationSpeed = R(120f, 150f);

		private float _angle = R(0f, 360f);

		private readonly float _scale = R(0.7f, 2.5f);

		private Color _color = new Color(R(0.2f, 0.4f), R(0.6f, 0.8f), 1f, 0f);

		private readonly Sprite2D? _sprite;

		internal StsEmptyStanceParticleEffect(Vector2 p)
		{
			if (MountRoot(p))
			{
				_sprite = Sprite(StsAssets.Region("combat/blurWave"), Vector2.Zero, _color, _angle, additive: true);
				if (_sprite == null)
				{
					Failed = true;
				}
				else
				{
					Root.AddChild(_sprite, forceReadableName: false, Node.InternalMode.Disabled);
				}
			}
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			Vector2 vector = Vector2.FromAngle(Mathf.DegToRad(_angle)).Normalized() * 10f;
			if (_sprite != null)
			{
				_sprite.Position = -vector;
				_sprite.RotationDegrees = _angle;
				_sprite.Scale = Vector2.One * _scale;
				_sprite.Modulate = _color;
			}
			_angle += delta * _rotationSpeed;
			if (_duration > 0.3f)
			{
				_color.A = Fade(1f, 0f, (_duration - 0.3f) * 2f);
			}
			else
			{
				_color.A = Fade(0f, 1f, _duration * 2f);
			}
			_duration -= delta;
			return _duration >= 0f;
		}
	}

	private sealed class StsBlasphemyEyeOpenEffect : VfxAnimator
	{
		private static readonly float[] FrameNudgeY = new float[7] { 12f, 8f, 4f, 3f, 0f, 0f, 0f };

		private const float OpenDuration = 0.45f;

		private const float HoldDuration = 0.7f;

		private const float CloseDuration = 0.45f;

		private const float FadeDuration = 0.25f;

		private const float BaseScale = 2.2f;

		private static readonly Color GoldColor = new Color(1f, 0.84f, 0.35f);

		private readonly Sprite2D? _glow;

		private readonly Sprite2D? _eye;

		private readonly float _anchorW;

		private readonly float _anchorH;

		private float _time;

		private int _lastFrame = -1;

		internal StsBlasphemyEyeOpenEffect(Vector2 p)
		{
			Texture2D texture2D = StsAssets.EyeFrame(0);
			if (texture2D == null)
			{
				Failed = true;
			}
			else
			{
				if (!MountRoot(p))
				{
					return;
				}
				_anchorW = texture2D.GetWidth();
				_anchorH = texture2D.GetHeight();
				_glow = Sprite(texture2D, Vector2.Zero, new Color(GoldColor, 0f), 0f, additive: true);
				_eye = Sprite(texture2D, Vector2.Zero, new Color(GoldColor, 0f), 0f, additive: true);
				if (_eye == null)
				{
					Failed = true;
					return;
				}
				if (_glow != null)
				{
					_glow.Scale = Vector2.One * 2.2f * 1.25f;
					Root.AddChild(_glow, forceReadableName: false, Node.InternalMode.Disabled);
				}
				_eye.Scale = Vector2.One * 2.2f;
				Root.AddChild(_eye, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead || _eye == null)
			{
				return false;
			}
			_time += delta;
			if (_time >= 1.8499999f)
			{
				return false;
			}
			int num;
			float num2;
			float num3;
			if (_time < 0.45f)
			{
				num = Math.Min(6, (int)(_time / 0.45f * 7f));
				num2 = Fade(0f, 1f, _time / 0.15f);
				num3 = Pow2Out(1.98f, 2.2f, _time / 0.45f);
			}
			else if (_time < 1.15f)
			{
				num = 6;
				num2 = 1f;
				num3 = 2.2f * (1f + 0.04f * MathF.Sin((_time - 0.45f) * ((float)Math.PI * 2f) * 1.5f));
			}
			else if (_time < 1.5999999f)
			{
				float num4 = (_time - 0.45f - 0.7f) / 0.45f;
				num = Math.Max(0, 6 - (int)(num4 * 7f));
				num2 = 1f;
				num3 = 2.2f;
			}
			else
			{
				num = 0;
				num2 = Fade(1f, 0f, (_time - 0.45f - 0.7f - 0.45f) / 0.25f);
				num3 = 2.2f;
			}
			if (num != _lastFrame)
			{
				Texture2D texture2D = StsAssets.EyeFrame(num);
				if (texture2D != null)
				{
					_eye.Texture = texture2D;
					if (_glow != null)
					{
						_glow.Texture = texture2D;
					}
					_lastFrame = num;
				}
			}
			Texture2D texture = _eye.Texture;
			float num5 = ((float?)texture?.GetWidth()) ?? _anchorW;
			float num6 = ((float?)texture?.GetHeight()) ?? _anchorH;
			int num7 = Math.Clamp((_lastFrame >= 0) ? _lastFrame : 0, 0, 6);
			Vector2 position = new Vector2((0f - _anchorW) / 2f + num5 / 2f, _anchorH / 2f - FrameNudgeY[num7] - num6 / 2f);
			_eye.Position = position;
			_eye.Scale = Vector2.One * num3;
			Color goldColor = GoldColor;
			goldColor.A = num2;
			_eye.Modulate = goldColor;
			if (_glow != null)
			{
				_glow.Position = position;
				_glow.Scale = Vector2.One * num3 * 1.25f;
				Color goldColor2 = GoldColor;
				goldColor2.A = num2 * 0.35f;
				_glow.Modulate = goldColor2;
			}
			return true;
		}
	}

	private sealed class StsViolentAttackEffect : VfxAnimator
	{
		private int _count = 5;

		private float _duration;

		private readonly Vector2 _p;

		private readonly Color _color;

		internal StsViolentAttackEffect(Vector2 p, Color color)
		{
			_p = p;
			_color = color;
		}

		public override bool Advance(float delta)
		{
			_duration -= delta;
			if (_duration < 0f)
			{
				Emit(new StsAnimatedSlashEffect(_p + new Vector2(R(-100f, 100f), R(-100f, 100f)), Vector2.Zero, R(0f, 360f), R(2.5f, 4f), _color, _color));
				Emit(new StsSimpleSparkleEffect(_p + new Vector2(R(-150f, 150f), R(-150f, 150f)), _color));
				_duration = R(0.05f, 0.1f);
				_count--;
			}
			return _count > 0;
		}
	}

	private sealed class StsDivinityStanceChangeParticle : VfxAnimator
	{
		private float _duration = 0.5f;

		private float _delay = R(0f, 0.4f);

		private readonly float _rotation = R(0f, 360f);

		private readonly Vector2 _origin;

		private readonly float _distOffset = R(800f, 1200f);

		private readonly float _scaleOffset = R(4f, 5f);

		private Color _color;

		private readonly Sprite2D? _sprite;

		internal StsDivinityStanceChangeParticle(Color color, Vector2 p)
		{
			_color = color;
			_origin = p + new Vector2(R(-10f, 10f), R(-10f, 10f));
			if (MountRoot(_origin, 0f, behind: true))
			{
				_sprite = Sprite(StsAssets.Region("combat/strikeLine2"), Vector2.Zero, _color, _rotation, additive: true);
				if (_sprite == null)
				{
					Failed = true;
				}
				else
				{
					Root.AddChild(_sprite, forceReadableName: false, Node.InternalMode.Disabled);
				}
			}
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			if (_delay > 0f)
			{
				_delay -= delta;
				return true;
			}
			_duration -= delta;
			if (_duration < 0f)
			{
				return false;
			}
			float x = Mathf.DegToRad(_rotation);
			Root.Position = _origin + new Vector2(MathF.Cos(x) * _distOffset * Pow2In(0.02f, 0.95f, _duration * 2f), MathF.Sin(x) * _distOffset * Pow3In(0.02f, 0.95f, _duration * 2f));
			_duration -= delta;
			float num = _scaleOffset * (_duration + 0.1f);
			_color.A = Pow3In(0f, 1f, _duration * 2f);
			if (_sprite != null)
			{
				_sprite.Scale = Vector2.One * num;
				_sprite.Modulate = _color;
			}
			return true;
		}
	}

	private static class DivinityEyeDriver
	{
		private static readonly List<DivinityEyeInstance> Instances = new List<DivinityEyeInstance>();

		private static readonly List<DivinityEyeSweep> Sweeps = new List<DivinityEyeSweep>();

		private static bool _hooked;

		private static double _lastTime;

		internal static void Start(Creature owner)
		{
			EnsureHooked();
			Stop(owner);
			Instances.Add(new DivinityEyeInstance(owner));
			EyeDiag($"eye driver Start instances={Instances.Count} hooked={_hooked}");
		}

		internal static void Stop(Creature owner)
		{
			for (int num = Instances.Count - 1; num >= 0; num--)
			{
				if (Instances[num].Owner == owner)
				{
					Instances[num].Dispose();
					Instances.RemoveAt(num);
				}
			}
		}

		internal static void StartSweep(Creature owner)
		{
			EnsureHooked();
			StopSweep(owner);
			bool flag;
			try
			{
				flag = !LocalContext.NetId.HasValue || LocalContext.IsMe(owner);
			}
			catch
			{
				flag = false;
			}
			if (flag)
			{
				Sweeps.Add(new DivinityEyeSweep(owner));
			}
			EyeDiag($"deva sweep Start local={flag} sweeps={Sweeps.Count} hooked={_hooked}");
		}

		internal static void StopSweep(Creature owner)
		{
			for (int num = Sweeps.Count - 1; num >= 0; num--)
			{
				if (Sweeps[num].Owner == owner)
				{
					Sweeps[num].Dispose();
					Sweeps.RemoveAt(num);
				}
			}
		}

		private static void EnsureHooked()
		{
			if (!_hooked && Engine.GetMainLoop() is SceneTree sceneTree)
			{
				sceneTree.ProcessFrame += Tick;
				_hooked = true;
				_lastTime = (double)Time.GetTicksMsec() / 1000.0;
			}
		}

		private static void Tick()
		{
			double num = (double)Time.GetTicksMsec() / 1000.0;
			float num2 = (float)Math.Max(0.0, num - _lastTime);
			_lastTime = num;
			if (num2 > 0.1f)
			{
				num2 = 0.1f;
			}
			for (int num3 = Instances.Count - 1; num3 >= 0; num3--)
			{
				DivinityEyeInstance divinityEyeInstance = Instances[num3];
				try
				{
					if (divinityEyeInstance.ShouldStop())
					{
						divinityEyeInstance.Dispose();
						Instances.RemoveAt(num3);
					}
					else
					{
						divinityEyeInstance.Tick(num2);
					}
				}
				catch (Exception ex)
				{
					EyeDiag("eye tick failed: " + ex.Message);
					divinityEyeInstance.Dispose();
					Instances.RemoveAt(num3);
				}
			}
			for (int num4 = Sweeps.Count - 1; num4 >= 0; num4--)
			{
				DivinityEyeSweep divinityEyeSweep = Sweeps[num4];
				try
				{
					if (divinityEyeSweep.ShouldStop())
					{
						divinityEyeSweep.Dispose();
						Sweeps.RemoveAt(num4);
					}
					else
					{
						divinityEyeSweep.Tick(num2);
					}
				}
				catch (Exception ex2)
				{
					EyeDiag("eye sweep tick failed: " + ex2.Message);
					divinityEyeSweep.Dispose();
					Sweeps.RemoveAt(num4);
				}
			}
		}
	}

	private sealed class DivinityEyeInstance
	{
		private const int MaxConcurrent = 4;

		internal readonly Creature Owner;

		private readonly List<EyeParticle> _particles = new List<EyeParticle>();

		private float _spawnTimer;

		private float _grace = 0.3f;

		private bool _firstSpawnLogged;

		internal DivinityEyeInstance(Creature owner)
		{
			Owner = owner;
		}

		internal bool ShouldStop()
		{
			if (NonInteractiveMode.IsActive)
			{
				return true;
			}
			if (NCombatRoom.Instance == null)
			{
				return true;
			}
			if (_grace > 0f)
			{
				return false;
			}
			try
			{
				return !WatcherCombatHelper.IsInStance<Divinity>(Owner);
			}
			catch
			{
				return true;
			}
		}

		internal void Tick(float delta)
		{
			if (_grace > 0f)
			{
				_grace -= delta;
			}
			_spawnTimer -= delta;
			if (_spawnTimer <= 0f && _particles.Count < 4)
			{
				EyeParticle eyeParticle = EyeParticle.Spawn(CreatureCenter(Owner), R(1f, 1.5f));
				if (eyeParticle != null)
				{
					_particles.Add(eyeParticle);
					if (!_firstSpawnLogged)
					{
						_firstSpawnLogged = true;
						EyeDiag($"eye first particle ok base={eyeParticle.BaseWorld} live={_particles.Count}");
					}
				}
				_spawnTimer = R(0.35f, 0.6f);
			}
			for (int num = _particles.Count - 1; num >= 0; num--)
			{
				_particles[num].Advance(delta);
				if (_particles[num].Dead)
				{
					_particles.RemoveAt(num);
				}
			}
		}

		internal void Dispose()
		{
			foreach (EyeParticle particle in _particles)
			{
				particle.Free();
			}
			_particles.Clear();
		}
	}

	private sealed class DivinityEyeSweep
	{
		private sealed class Eye
		{
			internal Sprite2D Sprite;

			internal float EyeScale;

			internal float BlinkOffset;

			internal float AnchorW;

			internal float AnchorH;

			internal Color BaseColor;
		}

		private static readonly Texture2D?[] Frames = Enumerable.Range(0, 7).Select(StsAssets.EyeFrame).ToArray();

		private static readonly float[] FrameNudgeY = new float[7] { 12f, 8f, 4f, 3f, 0f, 0f, 0f };

		private const int EyesPerRay = 4;

		private const float RaySpacingDeg = 30f;

		private const float DegPerSecond = 12f;

		private const float EdgeFadeDeg = 14f;

		private const float MaxAlpha = 0.55f;

		private const float BlinkPeriod = 0.65f;

		private const float BlinkDuty = 0.45f;

		private static readonly float[] RayRadii = new float[4] { 55f, 95f, 135f, 175f };

		private static readonly float[] RayScales = new float[4] { 0.55f, 0.66f, 0.78f, 0.9f };

		private static readonly Color NearColor = new Color(1f, 0.84f, 0.35f);

		private static readonly Color FarColor = new Color(0.72f, 0.3f, 0.95f);

		internal readonly Creature Owner;

		private readonly System.Collections.Generic.Dictionary<int, Eye?[]> _rays = new System.Collections.Generic.Dictionary<int, Eye[]>();

		private Node2D? _root;

		private float _time;

		private float _rotation;

		private float _grace = 0.3f;

		private bool _done;

		private bool _firstSpawnLogged;

		internal DivinityEyeSweep(Creature owner)
		{
			Owner = owner;
		}

		internal bool ShouldStop()
		{
			if (_done)
			{
				return true;
			}
			if (NonInteractiveMode.IsActive)
			{
				return true;
			}
			if (NCombatRoom.Instance == null)
			{
				return true;
			}
			if (_grace > 0f)
			{
				return false;
			}
			try
			{
				return !Owner.HasPower<DevaPower>();
			}
			catch
			{
				return true;
			}
		}

		internal void Tick(float delta)
		{
			if (_grace > 0f)
			{
				_grace -= delta;
			}
			if (!EnsureRoot())
			{
				return;
			}
			_time += delta;
			_rotation += 12f * delta;
			int nMin = Math.Max(0, (int)MathF.Ceiling((_rotation - 180f) / 30f));
			int nMax = (int)MathF.Floor(_rotation / 30f);
			if (_rays.Count > 0)
			{
				foreach (int item in _rays.Keys.Where((int k) => k < nMin || k > nMax).ToList())
				{
					FreeRay(item);
				}
			}
			for (int i = nMin; i <= nMax; i++)
			{
				float num = 180f + (float)i * 30f - _rotation;
				if (num < 0f || num > 180f)
				{
					continue;
				}
				if (!_rays.TryGetValue(i, out Eye[] value))
				{
					value = SpawnRay(i);
				}
				if (value == null)
				{
					continue;
				}
				float alpha = 0.55f * Clamp01(MathF.Min(num, 180f - num) / 14f);
				float x = Mathf.DegToRad(num);
				Vector2 vector = new Vector2(MathF.Cos(x), 0f - MathF.Sin(x));
				for (int j = 0; j < 4; j++)
				{
					Eye eye = value[j];
					if (eye != null && GodotObject.IsInstanceValid(eye.Sprite))
					{
						float num2 = (_time + eye.BlinkOffset) % 0.65f / 0.65f;
						int frame = 6;
						if (num2 < 0.45f)
						{
							float num3 = num2 / 0.45f;
							float num4 = ((num3 < 0.5f) ? (num3 * 2f) : ((1f - num3) * 2f));
							frame = 6 - (int)MathF.Round(num4 * 6f);
						}
						ApplyFrame(eye, vector * RayRadii[j], frame, alpha, num);
					}
				}
			}
		}

		internal void Dispose()
		{
			foreach (int item in _rays.Keys.ToList())
			{
				FreeRay(item);
			}
			if (_root != null && GodotObject.IsInstanceValid(_root))
			{
				_root.QueueFree();
			}
			_root = null;
			_done = true;
		}

		private bool EnsureRoot()
		{
			if (_root != null)
			{
				return GodotObject.IsInstanceValid(_root);
			}
			Node2D node2D = (NCombatRoom.Instance?.GetCreatureNode(Owner))?.Visuals;
			if (node2D == null)
			{
				return false;
			}
			_root = new Node2D
			{
				Name = "DivinityEyeSweep",
				Position = new Vector2(0f, -110f)
			};
			node2D.AddChild(_root, forceReadableName: false, Node.InternalMode.Disabled);
			node2D.MoveChild(_root, 0);
			EyeDiag($"eye sweep root mounted visualsScale={node2D.Scale} visualsPos={node2D.GlobalPosition}");
			return true;
		}

		private Eye?[]? SpawnRay(int index)
		{
			if (_root == null)
			{
				return null;
			}
			Eye[] array = new Eye[4];
			for (int i = 0; i < 4; i++)
			{
				Sprite2D sprite2D = Sprite(Frames[6] ?? Frames[0], Vector2.Zero, new Color(1f, 1f, 1f, 0f), 0f, additive: true);
				if (sprite2D != null)
				{
					_root.AddChild(sprite2D, forceReadableName: false, Node.InternalMode.Disabled);
					float weight = (float)i / 3f;
					Eye eye = new Eye
					{
						Sprite = sprite2D,
						EyeScale = RayScales[i] * R(0.95f, 1.05f),
						BlinkOffset = R(0f, 0.65f),
						AnchorW = (((float?)Frames[0]?.GetWidth()) ?? 62f),
						AnchorH = (((float?)Frames[0]?.GetHeight()) ?? 25f),
						BaseColor = NearColor.Lerp(FarColor, weight)
					};
					sprite2D.Scale = Vector2.One * eye.EyeScale;
					array[i] = eye;
				}
			}
			_rays[index] = array;
			if (!_firstSpawnLogged)
			{
				_firstSpawnLogged = true;
				EyeDiag($"eye fan first ray ok index={index} eyes={array.Count((Eye e) => e != null)}");
			}
			return array;
		}

		private void ApplyFrame(Eye eye, Vector2 arcPos, int frame, float alpha, float angleDeg)
		{
			Texture2D texture2D = Frames[frame] ?? Frames[0];
			if (texture2D != null)
			{
				float num = texture2D.GetWidth();
				float num2 = texture2D.GetHeight();
				eye.Sprite.Texture = texture2D;
				eye.Sprite.Position = arcPos + new Vector2((0f - eye.AnchorW) / 2f + num / 2f, eye.AnchorH / 2f - FrameNudgeY[frame] - num2 / 2f);
				eye.Sprite.RotationDegrees = (90f - angleDeg) * 0.35f;
				Color baseColor = eye.BaseColor;
				baseColor.A = alpha;
				eye.Sprite.Modulate = baseColor;
			}
		}

		private void FreeRay(int index)
		{
			if (!_rays.TryGetValue(index, out Eye[] value))
			{
				return;
			}
			_rays.Remove(index);
			Eye[] array = value;
			foreach (Eye eye in array)
			{
				if (eye != null && GodotObject.IsInstanceValid(eye.Sprite))
				{
					eye.Sprite.QueueFree();
				}
			}
		}
	}

	private sealed class EyeParticle
	{
		private static readonly Texture2D?[] Frames = Enumerable.Range(0, 7).Select(StsAssets.EyeFrame).ToArray();

		private readonly Sprite2D _sprite;

		private readonly Vector2 _base;

		private readonly float _startingDuration;

		private readonly float _durDiv2;

		private readonly float _anchorWidth;

		private readonly float _anchorHeight;

		private float _duration;

		private Color _color;

		internal bool Dead { get; private set; }

		internal Vector2 BaseWorld => _base;

		private EyeParticle(Sprite2D sprite, Vector2 center, Vector2 offset, float scale)
		{
			_sprite = sprite;
			_base = center + offset;
			_startingDuration = scale + 0.8f;
			_durDiv2 = _startingDuration / 2f;
			_duration = _startingDuration;
			_anchorWidth = ((float?)Frames[0]?.GetWidth()) ?? 62f;
			_anchorHeight = ((float?)Frames[0]?.GetHeight()) ?? 25f;
			_color = new Color(R(0.9f, 1f), R(0.55f, 0.75f), R(0.9f, 1f), 0f);
			_sprite.ZIndex = 50;
			_sprite.Scale = Vector2.One * scale;
			_sprite.RotationDegrees = R(6f, 12f) * ((offset.X > 0f) ? (-1f) : 1f);
			Advance(0f);
		}

		internal static EyeParticle? Spawn(Vector2 center, float scale)
		{
			Sprite2D sprite2D = Sprite(Frames[0], Vector2.Zero, new Color(1f, 1f, 1f, 0f), 0f, additive: true);
			if (sprite2D == null)
			{
				return null;
			}
			if (!TryAddToCombatVfx(sprite2D))
			{
				return null;
			}
			Vector2 offset = new Vector2(R(-110f, 110f), R(-80f, 80f));
			return new EyeParticle(sprite2D, center, offset, scale);
		}

		internal void Advance(float delta)
		{
			if (Dead)
			{
				return;
			}
			if (!GodotObject.IsInstanceValid(_sprite))
			{
				Dead = true;
				return;
			}
			float num = _duration / _startingDuration;
			int frame;
			float vY;
			if (num > 0.85f)
			{
				frame = 0;
				vY = 12f;
			}
			else if (num > 0.8f)
			{
				frame = 1;
				vY = 8f;
			}
			else if (num > 0.75f)
			{
				frame = 2;
				vY = 4f;
			}
			else if (num > 0.7f)
			{
				frame = 3;
				vY = 3f;
			}
			else if (num > 0.65f)
			{
				frame = 4;
				vY = 0f;
			}
			else if (num > 0.6f)
			{
				frame = 5;
				vY = 0f;
			}
			else if (num > 0.55f)
			{
				frame = 6;
				vY = 0f;
			}
			else if (num > 0.38f)
			{
				frame = 5;
				vY = 0f;
			}
			else if (num > 0.3f)
			{
				frame = 4;
				vY = 0f;
			}
			else if (num > 0.25f)
			{
				frame = 3;
				vY = 3f;
			}
			else if (num > 0.2f)
			{
				frame = 2;
				vY = 4f;
			}
			else if (num > 0.15f)
			{
				frame = 1;
				vY = 8f;
			}
			else
			{
				frame = 0;
				vY = 12f;
			}
			_color.A = ((_duration > _durDiv2) ? Fade(1f, 0f, (_duration - _durDiv2) / _durDiv2) : Fade(0f, 1f, _duration / _durDiv2));
			ApplyFrame(frame, vY);
			_duration -= delta;
			if (_duration < 0f)
			{
				Free();
			}
		}

		internal void Free()
		{
			if (!Dead)
			{
				Dead = true;
				if (GodotObject.IsInstanceValid(_sprite))
				{
					_sprite.QueueFree();
				}
			}
		}

		private void ApplyFrame(int frame, float vY)
		{
			Texture2D texture2D = Frames[frame] ?? Frames[0];
			if (texture2D != null)
			{
				float num = texture2D.GetWidth();
				float num2 = texture2D.GetHeight();
				_sprite.Texture = texture2D;
				_sprite.Position = _base + new Vector2((0f - _anchorWidth) / 2f + num / 2f, _anchorHeight / 2f - vY - num2 / 2f);
				_sprite.Modulate = _color;
			}
		}
	}

	private sealed class StsSimpleSparkleEffect : VfxAnimator
	{
		private float _duration = 0.35f;

		private readonly Sprite2D? _sprite;

		private Color _color;

		internal StsSimpleSparkleEffect(Vector2 p)
			: this(p, Colors.White)
		{
		}

		internal StsSimpleSparkleEffect(Vector2 p, Color color)
		{
			_color = color;
			if (MountRoot(p))
			{
				_sprite = Sprite(StsAssets.Region("shine1") ?? StsAssets.Region("combat/tinyStar2"), Vector2.Zero, _color, R(0f, 360f), additive: true);
				if (_sprite == null)
				{
					Failed = true;
					return;
				}
				_sprite.Scale = Vector2.One * R(0.5f, 1.4f);
				Root.AddChild(_sprite, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			_duration -= delta;
			_color.A = Clamp01(_duration / 0.35f);
			if (_sprite != null)
			{
				_sprite.Modulate = _color;
			}
			return _duration >= 0f;
		}
	}

	private sealed class StsEnergyBladeEffect : VfxAnimator
	{
		private const string BladePath = "res://images/vfx/watcher/expunger_blade.png";

		private const float BladeScale = 0.8f;

		private const float GripFromCentre = 26f;

		private const float DrawSeconds = 0.12f;

		private const float SheatheSeconds = 0.14f;

		private readonly Creature _caster;

		private readonly double _life;

		private readonly double _start;

		private readonly Sprite2D? _sword;

		private readonly Sprite2D? _bloom;

		private readonly Sprite2D? _hiltFlare;

		internal StsEnergyBladeEffect(Creature caster, double life)
		{
			_caster = caster;
			_life = Math.Max(0.25999999046325684, life);
			_start = (double)Time.GetTicksMsec() / 1000.0;
			Texture2D texture2D = WatcherTextureHelper.LoadTexture("res://images/vfx/watcher/expunger_blade.png");
			if (texture2D == null)
			{
				Failed = true;
			}
			else
			{
				if (!MountRoot(StaffGripPosition(caster)))
				{
					return;
				}
				_bloom = Sprite(texture2D, Vector2.Zero, new Color(BladeGlow, 0f), 0f, additive: true);
				_sword = Sprite(texture2D, Vector2.Zero, new Color(1f, 1f, 1f, 0f));
				_hiltFlare = Sprite(StsAssets.Region("combat/empowerCircle1") ?? StsAssets.Region("shine1"), Vector2.Zero, new Color(BladeGlow, 0f), 0f, additive: true);
				Sprite2D[] array = new Sprite2D[3] { _bloom, _sword, _hiltFlare };
				foreach (Sprite2D sprite2D in array)
				{
					if (sprite2D != null)
					{
						sprite2D.Offset = new Vector2(0f, 26f);
						Root.AddChild(sprite2D, forceReadableName: false, Node.InternalMode.Disabled);
					}
				}
				if (_sword == null)
				{
					Failed = true;
				}
			}
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead || _sword == null)
			{
				return false;
			}
			double num = (double)Time.GetTicksMsec() / 1000.0 - _start;
			if (num >= _life)
			{
				return false;
			}
			Root.Position = StaffGripPosition(_caster);
			float rotation = StaffAxis(_caster).Angle() - (float)Math.PI / 2f;
			float num2 = ((num < 0.11999999731779099) ? Pow2Out(0f, 1f, (float)(num / 0.11999999731779099)) : ((!(num > _life - 0.14000000059604645)) ? 1f : Pow2Out(0f, 1f, (float)((_life - num) / 0.14000000059604645))));
			Vector2 vector = new Vector2(0.8f * Lerp(0.55f, 1f, num2), 0.8f * Lerp(0.12f, 1f, num2));
			float num3 = 0.94f + 0.06f * MathF.Sin((float)num * 41f);
			_sword.Rotation = rotation;
			_sword.Scale = vector;
			_sword.Modulate = new Color(1f, 1f, 1f, num2);
			if (_bloom != null && GodotObject.IsInstanceValid(_bloom))
			{
				_bloom.Rotation = rotation;
				_bloom.Scale = vector * 1.06f;
				_bloom.Modulate = new Color(BladeGlow, 0.5f * num2 * num3);
			}
			if (_hiltFlare != null && GodotObject.IsInstanceValid(_hiltFlare))
			{
				_hiltFlare.Rotation = rotation;
				_hiltFlare.Offset = Vector2.Zero;
				_hiltFlare.Scale = Vector2.One * (1f + 0.45f * num2) * num3;
				_hiltFlare.Modulate = new Color(BladeGlow, 0.55f * num2);
			}
			return true;
		}
	}

	private sealed class StsBladeCutEffect : VfxAnimator
	{
		private const float Life = 0.3f;

		private const float Reach = 300f;

		private readonly Vector2 _dir;

		private readonly Line2D? _glow;

		private readonly Line2D? _core;

		private float _time;

		internal StsBladeCutEffect(Vector2 at, Vector2 bladeAxis)
		{
			_dir = bladeAxis.Rotated(Mathf.DegToRad(72f)).Normalized();
			if (MountRoot(at))
			{
				_glow = MakeCut(40f, new Color(BladeGlow, 0.55f));
				_core = MakeCut(12f, new Color(BladeCore));
				if (_glow == null && _core == null)
				{
					Failed = true;
				}
			}
		}

		private Line2D? MakeCut(float width, Color color)
		{
			if (Root == null)
			{
				return null;
			}
			Curve curve = new Curve
			{
				MinValue = 0f,
				MaxValue = 1f
			};
			curve.AddPoint(new Vector2(0f, 0.05f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			curve.AddPoint(new Vector2(0.5f, 1f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			curve.AddPoint(new Vector2(1f, 0.05f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			Line2D line2D = new Line2D
			{
				Width = width,
				DefaultColor = color,
				WidthCurve = curve,
				JointMode = Line2D.LineJointMode.Round,
				BeginCapMode = Line2D.LineCapMode.Round,
				EndCapMode = Line2D.LineCapMode.Round,
				Antialiased = true,
				Material = new CanvasItemMaterial
				{
					BlendMode = CanvasItemMaterial.BlendModeEnum.Add
				}
			};
			Root.AddChild(line2D, forceReadableName: false, Node.InternalMode.Disabled);
			return line2D;
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			_time += delta;
			if (_time >= 0.3f)
			{
				return false;
			}
			float num = _time / 0.3f;
			float num2 = 300f * Pow2Out(0.3f, 1f, num) * 0.5f;
			Vector2[] points = new Vector2[2]
			{
				-_dir * num2,
				_dir * num2
			};
			StsLightningBolt.SetPoints(_glow, points);
			StsLightningBolt.SetPoints(_core, points);
			float num3 = Exp5In(0f, 1f, 1f - num);
			if (_glow != null)
			{
				_glow.Modulate = new Color(1f, 1f, 1f, num3 * 0.6f);
			}
			if (_core != null)
			{
				_core.Modulate = new Color(1f, 1f, 1f, num3);
			}
			return true;
		}
	}

	private readonly record struct JudgmentImpactContext(Creature Caster, Vector2 Hit, Vector2 TextAt, float Scale, string Title);

	private static class HammerShape
	{
		private const float HeadTexW = 251f;

		private const float HeadTexH = 170f;

		private const float ShaftTexH = 683f;

		internal const float HeadScale = 1.45f;

		internal const float ShaftScaleY = 0.75f;

		private const float ShaftOverlap = 34f;

		internal const float HeadHalfWidth = 181.975f;

		internal const float HeadHeight = 246.50002f;

		internal const float ShaftLength = 512.25f;

		internal static readonly Vector2 HeadCentre = new Vector2(0f, -123.25001f);

		internal static readonly Vector2 ShaftCentre = new Vector2(0f, -468.625f);

		private static Texture2D? HeadTexture => StsAssets.Region("combat/weightyImpact") ?? StsAssets.Region("combat/empowerCircle1");

		private static Texture2D? ShaftTexture => StsAssets.Region("combat/verticalImpact") ?? StsAssets.Region("combat/strikeLine2");

		internal static Sprite2D? Head(Node2D root, Color color, float scale)
		{
			Sprite2D sprite2D = Sprite(HeadTexture, HeadCentre, color, 0f, additive: true);
			if (sprite2D == null)
			{
				return null;
			}
			sprite2D.Scale = Vector2.One * 1.45f * scale;
			root.AddChild(sprite2D, forceReadableName: false, Node.InternalMode.Disabled);
			return sprite2D;
		}

		internal static Sprite2D? Shaft(Node2D root, Color color, float widthScale)
		{
			Sprite2D sprite2D = Sprite(ShaftTexture, ShaftCentre, color, 0f, additive: true);
			if (sprite2D == null)
			{
				return null;
			}
			sprite2D.Scale = new Vector2(widthScale, 0.75f);
			root.AddChild(sprite2D, forceReadableName: false, Node.InternalMode.Disabled);
			return sprite2D;
		}

		internal static void Tint(Sprite2D? s, Color color, float alpha)
		{
			if (s != null && GodotObject.IsInstanceValid(s))
			{
				s.Modulate = new Color(color, Math.Clamp(alpha, 0f, 1f));
			}
		}
	}

	private sealed class StsJudgmentHammerEffect : VfxAnimator
	{
		private const float GhostSpacing = 90f;

		private const float SquashSeconds = 0.09f;

		private const float SettleSeconds = 0.16f;

		private readonly Vector2 _start;

		private readonly Vector2 _hit;

		private readonly float _scale;

		private readonly JudgmentImpactContext _impact;

		private readonly Sprite2D? _headHaze;

		private readonly Sprite2D? _headGlow;

		private readonly Sprite2D? _headCore;

		private readonly Sprite2D? _shaftHaze;

		private readonly Sprite2D? _shaftGlow;

		private readonly Sprite2D? _shaftCore;

		private readonly Sprite2D? _emblem;

		private readonly Sprite2D? _emblemCore;

		private readonly Sprite2D? _face;

		private readonly float _tilt = R(-2f, 2f);

		private float _time;

		private bool _landed;

		private float _sinceLanded;

		private float _lastGhostY;

		private float _moteTimer;

		internal StsJudgmentHammerEffect(Vector2 hit, float scale, JudgmentImpactContext impact)
		{
			_hit = hit;
			_scale = scale;
			_impact = impact;
			_start = new Vector2(hit.X, -246.50002f * scale - 60f);
			_lastGhostY = _start.Y;
			if (MountRoot(_start, _tilt))
			{
				Node2D root = Root;
				root.Scale = new Vector2(0.92f, 1.2f) * scale;
				_headHaze = HammerShape.Head(root, new Color(JudgeViolet, 0.3f), 1.2f);
				_shaftHaze = HammerShape.Shaft(root, new Color(JudgeViolet, 0.4f), 0.5f);
				_shaftGlow = HammerShape.Shaft(root, new Color(JudgeGold, 0.9f), 0.26f);
				_shaftCore = HammerShape.Shaft(root, new Color(1f, 0.98f, 0.9f, 0.8f), 0.1f);
				_headGlow = HammerShape.Head(root, new Color(JudgeGold), 1f);
				_headCore = HammerShape.Head(root, new Color(1f, 0.97f, 0.85f, 0.3f), 0.6f);
				Texture2D texture2D = StsAssets.Region("combat/empowerCircle1") ?? StsAssets.Region("shine1");
				Texture2D texture2D2 = StsAssets.Region("shine1") ?? texture2D;
				_emblem = Sprite(texture2D, HammerShape.HeadCentre, new Color(JudgeGold, 0.6f), 0f, additive: true);
				if (_emblem != null)
				{
					_emblem.Scale = Vector2.One * 1.7f;
					root.AddChild(_emblem, forceReadableName: false, Node.InternalMode.Disabled);
				}
				_emblemCore = Sprite(texture2D2, HammerShape.HeadCentre, new Color(1f, 1f, 0.9f, 0.6f), 0f, additive: true);
				if (_emblemCore != null)
				{
					_emblemCore.Scale = Vector2.One * 0.85f;
					root.AddChild(_emblemCore, forceReadableName: false, Node.InternalMode.Disabled);
				}
				_face = Sprite(StsAssets.Region("combat/strikeLine2") ?? texture2D2, Vector2.Zero, new Color(1f, 0.97f, 0.85f, 0f), 0f, additive: true);
				if (_face != null)
				{
					_face.Scale = new Vector2(2.6f, 1.6f);
					root.AddChild(_face, forceReadableName: false, Node.InternalMode.Disabled);
				}
				if (_headGlow == null && _shaftGlow == null)
				{
					Failed = true;
				}
			}
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			Node2D root = Root;
			_time += delta;
			if (!_landed)
			{
				float num = Clamp01(_time / 0.36f);
				float num2 = Pow3In(_start.Y, _hit.Y, num);
				root.Position = new Vector2(_hit.X, num2);
				root.Scale = new Vector2(0.92f, 1.2f) * _scale;
				if (num2 - _lastGhostY >= 90f)
				{
					_lastGhostY = num2;
					Emit(new StsJudgmentGhostEffect(new Vector2(_hit.X, num2), root.Scale, _tilt));
				}
				if (num >= 1f)
				{
					_landed = true;
					_sinceLanded = 0f;
					root.Position = _hit;
					JudgmentImpact(in _impact);
				}
				return true;
			}
			_sinceLanded += delta;
			float num3;
			float num4;
			if (_sinceLanded < 0.09f)
			{
				float t = _sinceLanded / 0.09f;
				num3 = Pow2Out(0.92f, 1.16f, t);
				num4 = Pow2Out(1.2f, 0.82f, t);
			}
			else if (_sinceLanded < 0.25f)
			{
				float t2 = (_sinceLanded - 0.09f) / 0.16f;
				num3 = Fade(1.16f, 1f, t2);
				num4 = Fade(0.82f, 1.04f, t2);
			}
			else
			{
				float t3 = Clamp01((_sinceLanded - 0.09f - 0.16f) / 0.12f);
				num3 = 1f;
				num4 = Fade(1.04f, 1f, t3);
			}
			float num5 = 1f;
			float a = 1f;
			float num6 = 0.29000002f;
			if (_sinceLanded < num6)
			{
				float num7 = _sinceLanded / num6;
				num5 = 1f + 0.9f * (1f - num7) + 0.15f * MathF.Sin(_sinceLanded * ((float)Math.PI * 2f) * 5f);
				if (_face != null && GodotObject.IsInstanceValid(_face))
				{
					_face.Modulate = new Color(1f, 0.97f, 0.85f, Exp5In(0f, 1f, 1f - num7));
					_face.Scale = new Vector2(Lerp(2.6f, 3.8f, 1f - num7), Lerp(1.6f, 2.6f, 1f - num7));
				}
			}
			else
			{
				float num8 = Clamp01((_sinceLanded - num6) / 0.3f);
				if (num8 >= 1f)
				{
					return false;
				}
				root.Position = _hit - new Vector2(0f, Pow2In(0f, 200f, num8));
				a = Fade(1f, 0f, num8);
				num5 = 1f + 1.2f * num8;
				num3 = Lerp(num3, 1f + 0.08f * num8, 1f);
				num4 = Lerp(num4, 1f + 0.12f * num8, 1f);
				_moteTimer -= delta;
				if (_moteTimer <= 0f)
				{
					_moteTimer = 0.035f;
					Emit(new StsJudgmentMoteEffect(root.Position + new Vector2(R(-181.975f, 181.975f) * _scale, (0f - R(0f, 246.50002f)) * _scale), R(160f, 420f), RB(0.3) ? Colors.White : JudgeGold));
				}
			}
			root.Scale = new Vector2(num3, num4) * _scale;
			root.Modulate = new Color(1f, 1f, 1f, a);
			float num9 = Math.Min(num5, 1.5f);
			HammerShape.Tint(_headHaze, JudgeViolet, 0.3f * num9);
			HammerShape.Tint(_headGlow, JudgeGold, 1f);
			HammerShape.Tint(_headCore, new Color(1f, 0.97f, 0.85f), 0.3f * num9);
			HammerShape.Tint(_shaftHaze, JudgeViolet, 0.4f * Math.Min(num5, 1.3f));
			HammerShape.Tint(_shaftGlow, JudgeGold, 0.9f);
			HammerShape.Tint(_shaftCore, new Color(1f, 0.98f, 0.9f), 0.8f);
			if (_headHaze != null && GodotObject.IsInstanceValid(_headHaze))
			{
				_headHaze.Scale = Vector2.One * 1.45f * 1.2f * (0.85f + 0.15f * num9);
			}
			if (_emblem != null && GodotObject.IsInstanceValid(_emblem))
			{
				_emblem.RotationDegrees += 90f * delta;
				_emblem.Modulate = new Color(JudgeGold, Math.Min(1f, 0.6f * Math.Min(num5, 1.25f)));
			}
			if (_emblemCore != null && GodotObject.IsInstanceValid(_emblemCore))
			{
				_emblemCore.Scale = Vector2.One * (0.85f + 0.2f * MathF.Sin(_time * ((float)Math.PI * 2f) * 4f));
			}
			return true;
		}
	}

	private sealed class StsJudgmentGhostEffect : VfxAnimator
	{
		private const float Life = 0.16f;

		private float _time;

		internal StsJudgmentGhostEffect(Vector2 p, Vector2 scale, float tilt)
		{
			if (MountRoot(p, tilt))
			{
				Node2D? root = Root;
				root.Scale = scale;
				HammerShape.Head(root, new Color(JudgeViolet, 0.24f), 1.1f);
				HammerShape.Head(root, new Color(JudgeGold, 0.22f), 1f);
				HammerShape.Shaft(root, new Color(JudgeViolet, 0.22f), 0.4f);
			}
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			_time += delta;
			if (_time >= 0.16f)
			{
				return false;
			}
			float num = _time / 0.16f;
			Root.Modulate = new Color(1f, 1f, 1f, Exp5In(0f, 1f, 1f - num));
			return true;
		}
	}

	private sealed class StsJudgmentMoteEffect : VfxAnimator
	{
		private readonly float _speed;

		private readonly float _life = R(0.35f, 0.6f);

		private readonly float _drift = R(-30f, 30f);

		private readonly Color _tint;

		private readonly Sprite2D? _sprite;

		private float _time;

		internal StsJudgmentMoteEffect(Vector2 p, float upSpeed, Color tint)
		{
			_speed = upSpeed;
			_tint = tint;
			if (MountRoot(p, R(0f, 360f)))
			{
				_sprite = Sprite(StsAssets.Region("shine1") ?? StsAssets.Region("combat/tinyStar2"), Vector2.Zero, new Color(tint, 0f), 0f, additive: true);
				if (_sprite == null)
				{
					Failed = true;
					return;
				}
				_sprite.Scale = Vector2.One * R(0.25f, 0.6f);
				Root.AddChild(_sprite, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead || _sprite == null)
			{
				return false;
			}
			_time += delta;
			if (_time >= _life)
			{
				return false;
			}
			float num = _time / _life;
			Root.Position += new Vector2(_drift, 0f - _speed) * delta;
			float num2 = ((num < 0.2f) ? (num / 0.2f) : (1f - (num - 0.2f) / 0.8f));
			_sprite.Modulate = new Color(_tint, num2 * 0.9f);
			return true;
		}
	}

	private sealed class StsJudgmentTextEffect : VfxAnimator
	{
		private const float Life = 1f;

		private const float SlamSeconds = 0.14f;

		private const float FadeStart = 0.7f;

		private const int FontSize = 92;

		private static readonly Vector2 Box = new Vector2(1200f, 220f);

		private static readonly Color TextColor = new Color(1f, 0.98f, 0.94f);

		private readonly Label? _bloom;

		private readonly Label? _face;

		private readonly Vector2 _origin;

		private float _time;

		internal StsJudgmentTextEffect(Vector2 p, string title)
		{
			_origin = p;
			if (string.IsNullOrWhiteSpace(title) || !MountRoot(p))
			{
				Failed = true;
				return;
			}
			Font font = LoadTitleFont();
			_bloom = MakeLabel(title, font, new Color(TextColor, 0.35f), 0, additive: true);
			_face = MakeLabel(title, font, TextColor, 12, additive: false);
			if (_face == null)
			{
				Failed = true;
				return;
			}
			if (_bloom != null)
			{
				Root.AddChild(_bloom, forceReadableName: false, Node.InternalMode.Disabled);
			}
			Root.AddChild(_face, forceReadableName: false, Node.InternalMode.Disabled);
			Root.Modulate = new Color(1f, 1f, 1f, 0f);
		}

		private static Label? MakeLabel(string text, Font? font, Color color, int outline, bool additive)
		{
			try
			{
				Label label = new Label
				{
					Text = text,
					HorizontalAlignment = HorizontalAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center,
					MouseFilter = Control.MouseFilterEnum.Ignore,
					Size = Box,
					Position = -Box / 2f
				};
				if (font != null)
				{
					label.AddThemeFontOverride("font", font);
				}
				try
				{
					label.ApplyLocaleFontSubstitution(FontType.Bold, "font");
				}
				catch
				{
				}
				label.AddThemeFontSizeOverride("font_size", 92);
				label.AddThemeColorOverride("font_color", color);
				label.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.03f, 0.08f));
				label.AddThemeConstantOverride("outline_size", outline);
				if (additive)
				{
					label.Material = new CanvasItemMaterial
					{
						BlendMode = CanvasItemMaterial.BlendModeEnum.Add
					};
				}
				return label;
			}
			catch
			{
				return null;
			}
		}

		private static Font? LoadTitleFont()
		{
			try
			{
				if (ResourceLoader.Exists("res://themes/kreon_bold_shared.tres"))
				{
					return ResourceLoader.Load<Font>("res://themes/kreon_bold_shared.tres", null, ResourceLoader.CacheMode.Reuse);
				}
			}
			catch
			{
			}
			return null;
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			_time += delta;
			if (_time >= 1f)
			{
				return false;
			}
			Vector2 vector = Vector2.Zero;
			float num2;
			float a;
			if (_time < 0.14f)
			{
				float num = _time / 0.14f;
				float t = 1f - MathF.Pow(1f - num, 5f);
				num2 = Lerp(2f, 1f, t);
				a = Clamp01(num * 2.5f);
			}
			else if (_time < 0.7f)
			{
				num2 = 1f;
				a = 1f;
			}
			else
			{
				float num3 = (_time - 0.7f) / 0.3f;
				num2 = 1f + 0.08f * num3;
				a = Fade(1f, 0f, num3);
				vector = new Vector2(0f, -30f * num3);
			}
			Root.Scale = Vector2.One * num2;
			Root.Position = _origin + vector;
			Root.Modulate = new Color(1f, 1f, 1f, a);
			return true;
		}
	}

	private sealed class StsLightningBolt : VfxAnimator
	{
		private const float RefreshInterval = 0.035f;

		private readonly Creature _caster;

		private readonly Vector2 _end;

		private readonly float _life;

		private readonly float _widthScale;

		private readonly int _forkCount;

		private readonly Line2D[] _trunk = new Line2D[3];

		private readonly Line2D[] _forks;

		private float _time;

		private float _refresh;

		internal StsLightningBolt(Creature caster, Vector2 end, float life, float widthScale, int forks)
		{
			_caster = caster;
			_end = end;
			_life = life;
			_widthScale = widthScale;
			_forkCount = Math.Max(0, forks);
			_forks = new Line2D[_forkCount * 2];
			if (MountRoot(StaffEyePosition(caster)))
			{
				_trunk[0] = MakeLine(62f * widthScale, BoltOuter, 0.45f);
				_trunk[1] = MakeLine(36f * widthScale, BoltMid, 0.8f);
				_trunk[2] = MakeLine(14f * widthScale, BoltCore, 1f);
				for (int i = 0; i < _forkCount; i++)
				{
					_forks[i * 2] = MakeLine(30f * widthScale, BoltOuter, 0.4f);
					_forks[i * 2 + 1] = MakeLine(10f * widthScale, BoltCore, 0.9f);
				}
				Rebuild();
			}
		}

		private Line2D MakeLine(float width, Color color, float alpha)
		{
			Line2D line2D = new Line2D
			{
				Width = Math.Max(1f, width),
				DefaultColor = new Color(color, alpha),
				JointMode = Line2D.LineJointMode.Round,
				BeginCapMode = Line2D.LineCapMode.Round,
				EndCapMode = Line2D.LineCapMode.Round,
				Antialiased = true,
				Material = new CanvasItemMaterial
				{
					BlendMode = CanvasItemMaterial.BlendModeEnum.Add
				}
			};
			Root.AddChild(line2D, forceReadableName: false, Node.InternalMode.Disabled);
			return line2D;
		}

		internal static Curve BuildWidthCurve(int knots, float tipScale)
		{
			Curve curve = new Curve
			{
				MinValue = 0f,
				MaxValue = 1.6f
			};
			curve.AddPoint(new Vector2(0f, R(1.15f, 1.5f)), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			for (int i = 1; i < knots; i++)
			{
				float num = (float)i / (float)knots;
				float num2 = Lerp(1.25f, tipScale, num);
				curve.AddPoint(new Vector2(num, Math.Clamp(num2 * R(0.55f, 1.4f), 0.12f, 1.6f)), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			}
			curve.AddPoint(new Vector2(1f, Math.Max(0.12f, tipScale * R(0.5f, 0.9f))), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			return curve;
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			_time += delta;
			if (_time >= _life)
			{
				return false;
			}
			Root.Position = StaffEyePosition(_caster);
			_refresh -= delta;
			if (_refresh <= 0f)
			{
				Rebuild();
				_refresh = 0.035f;
			}
			float num = _time / _life;
			float num2 = ((num < 0.12f) ? Pow2Out(0f, 1f, num / 0.12f) : Exp5In(0f, 1f, (1f - num) / 0.88f));
			float num3 = 0.82f + 0.18f * MathF.Sin(_time * 62f);
			Root.Modulate = new Color(1f, 1f, 1f, Clamp01(num2 * num3));
			return true;
		}

		private void Rebuild()
		{
			Vector2 position = Root.Position;
			Vector2 vector = _end - position;
			float num = vector.Length();
			if (num < 1f)
			{
				return;
			}
			Vector2 vector2 = vector / num;
			Vector2 vector3 = new Vector2(0f - vector2.Y, vector2.X);
			int num2 = Math.Clamp((int)(num / 45f), 6, 22);
			float num3 = Math.Clamp(num * 0.09f, 16f, 58f);
			Vector2[] array = new Vector2[num2 + 1];
			array[0] = Vector2.Zero;
			array[num2] = vector;
			for (int i = 1; i < num2; i++)
			{
				float num4 = (float)i / (float)num2;
				float num5 = MathF.Sin(num4 * (float)Math.PI);
				array[i] = vector2 * (num * num4) + vector3 * (num3 * num5 * R(-1f, 1f)) + vector2 * R(-7f, 7f);
			}
			SetPoints(_trunk, array);
			Curve widthCurve = BuildWidthCurve(Math.Max(3, num2 / 2), 0.45f);
			Line2D[] trunk = _trunk;
			foreach (Line2D line2D in trunk)
			{
				if (line2D != null && GodotObject.IsInstanceValid(line2D))
				{
					line2D.WidthCurve = widthCurve;
				}
			}
			for (int k = 0; k < _forkCount; k++)
			{
				int num6 = Math.Clamp((int)((float)num2 * R(0.22f, 0.75f)), 1, num2 - 1);
				Vector2 vector4 = array[num6];
				float num7 = num * (1f - (float)num6 / (float)num2);
				Vector2 vector5 = vector2.Rotated(Mathf.DegToRad(R(24f, 58f) * (RB() ? 1f : (-1f))));
				Vector2 to = vector4 + vector5 * num7 * R(0.3f, 0.62f);
				Vector2[] points = BuildFork(vector4, to);
				Curve widthCurve2 = BuildWidthCurve(4, 0.2f);
				SetPoints(_forks[k * 2], points);
				SetPoints(_forks[k * 2 + 1], points);
				trunk = new Line2D[2]
				{
					_forks[k * 2],
					_forks[k * 2 + 1]
				};
				foreach (Line2D line2D2 in trunk)
				{
					if (line2D2 != null && GodotObject.IsInstanceValid(line2D2))
					{
						line2D2.WidthCurve = widthCurve2;
					}
				}
			}
		}

		internal static Vector2[] BuildFork(Vector2 from, Vector2 to)
		{
			Vector2 vector = to - from;
			float num = vector.Length();
			int num2 = Math.Clamp((int)(num / 40f), 3, 9);
			Vector2 vector2 = ((num > 0.01f) ? (vector / num) : Vector2.Right);
			Vector2 vector3 = new Vector2(0f - vector2.Y, vector2.X);
			float num3 = Math.Clamp(num * 0.11f, 8f, 34f);
			Vector2[] array = new Vector2[num2 + 1];
			array[0] = from;
			array[num2] = to;
			for (int i = 1; i < num2; i++)
			{
				float num4 = (float)i / (float)num2;
				array[i] = from + vector2 * (num * num4) + vector3 * (num3 * MathF.Sin(num4 * (float)Math.PI) * R(-1f, 1f));
			}
			return array;
		}

		private static void SetPoints(Line2D[] lines, Vector2[] points)
		{
			for (int i = 0; i < lines.Length; i++)
			{
				SetPoints(lines[i], points);
			}
		}

		internal static void SetPoints(Line2D? line, Vector2[] points)
		{
			if (line != null && GodotObject.IsInstanceValid(line))
			{
				line.Points = points;
			}
		}
	}

	private sealed class StsEyeRadianceEffect : VfxAnimator
	{
		private const int MaxBolts = 7;

		private const int MoteCount = 12;

		private const float GrowFraction = 0.3f;

		private readonly Creature _caster;

		private readonly double _duration;

		private readonly double _start;

		private readonly Line2D?[] _outer = new Line2D[7];

		private readonly Line2D?[] _mid = new Line2D[7];

		private readonly Line2D?[] _core = new Line2D[7];

		private readonly Line2D?[] _forkOuter = new Line2D[7];

		private readonly Line2D?[] _forkCore = new Line2D[7];

		private readonly float[] _angle = new float[7];

		private readonly float[] _reach = new float[7];

		private readonly float[] _born = new float[7];

		private readonly bool[] _lit = new bool[7];

		private readonly Vector2[][] _path = new Vector2[7][];

		private readonly Vector2[][] _forkPath = new Vector2[7][];

		private readonly Curve?[] _taper = new Curve[7];

		private readonly Curve?[] _forkTaper = new Curve[7];

		private readonly Sprite2D?[] _motes = new Sprite2D[12];

		private readonly float[] _moteAngle = new float[12];

		private readonly float[] _motePhase = new float[12];

		private readonly Sprite2D? _glow;

		private readonly float _growSeconds;

		private float _scaledTime;

		private float _realTime;

		internal StsEyeRadianceEffect(Creature caster, double realSeconds)
		{
			_caster = caster;
			_duration = Math.Max(0.2, realSeconds);
			_growSeconds = (float)(_duration * 0.30000001192092896);
			_start = (double)Time.GetTicksMsec() / 1000.0;
			if (!MountRoot(StaffEyePosition(caster)))
			{
				return;
			}
			_glow = Sprite(StsAssets.Region("combat/empowerCircle1") ?? StsAssets.Region("shine1"), Vector2.Zero, new Color(ChargeColor, 0f), 0f, additive: true);
			if (_glow != null)
			{
				Root.AddChild(_glow, forceReadableName: false, Node.InternalMode.Disabled);
			}
			for (int i = 0; i < 7; i++)
			{
				_angle[i] = (float)Math.PI * 2f * (float)i / 7f + R(-0.3f, 0.3f);
				_reach[i] = R(0.7f, 1.35f);
				_outer[i] = MakeBolt(new Color(BoltOuter, 0.45f));
				_mid[i] = MakeBolt(new Color(BoltMid, 0.8f));
				_core[i] = MakeBolt(new Color(BoltCore));
				_forkOuter[i] = MakeBolt(new Color(BoltOuter, 0.4f));
				_forkCore[i] = MakeBolt(new Color(BoltCore, 0.9f));
				Vector2 vector = new Vector2(MathF.Cos(_angle[i]), MathF.Sin(_angle[i]));
				float num = 480f * _reach[i] * R(0.85f, 1.15f);
				_path[i] = StsLightningBolt.BuildFork(vector * 16f, vector * num);
				Vector2[] array = _path[i];
				Vector2 vector2 = array[Math.Clamp(array.Length / 2, 1, array.Length - 2)];
				float x = _angle[i] + Mathf.DegToRad(R(28f, 62f) * (RB() ? 1f : (-1f)));
				Vector2 to = vector2 + new Vector2(MathF.Cos(x), MathF.Sin(x)) * num * R(0.3f, 0.55f);
				_forkPath[i] = StsLightningBolt.BuildFork(vector2, to);
				_taper[i] = StsLightningBolt.BuildWidthCurve(4, 0.35f);
				_forkTaper[i] = StsLightningBolt.BuildWidthCurve(3, 0.18f);
			}
			Texture2D texture = StsAssets.Region("combat/tinyStar2") ?? StsAssets.Region("shine1");
			for (int j = 0; j < 12; j++)
			{
				_moteAngle[j] = R(0f, (float)Math.PI * 2f);
				_motePhase[j] = R(0f, 1f);
				Sprite2D sprite2D = Sprite(texture, Vector2.Zero, new Color(ChargeColor, 0f), 0f, additive: true);
				if (sprite2D != null)
				{
					sprite2D.Scale = Vector2.One * R(0.6f, 1.3f);
					Root.AddChild(sprite2D, forceReadableName: false, Node.InternalMode.Disabled);
					_motes[j] = sprite2D;
				}
			}
		}

		private Line2D? MakeBolt(Color color)
		{
			if (Root == null)
			{
				return null;
			}
			Line2D line2D = new Line2D
			{
				Width = 1f,
				DefaultColor = color,
				JointMode = Line2D.LineJointMode.Round,
				BeginCapMode = Line2D.LineCapMode.Round,
				EndCapMode = Line2D.LineCapMode.Round,
				Antialiased = true,
				Material = new CanvasItemMaterial
				{
					BlendMode = CanvasItemMaterial.BlendModeEnum.Add
				}
			};
			Root.AddChild(line2D, forceReadableName: false, Node.InternalMode.Disabled);
			return line2D;
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			double num = (double)Time.GetTicksMsec() / 1000.0 - _start;
			if (num >= _duration)
			{
				return false;
			}
			_scaledTime += delta;
			Math.Max(0f, (float)num - _realTime);
			_realTime = (float)num;
			Root.Position = StaffEyePosition(_caster);
			float num2 = Clamp01((float)(num / _duration));
			float num3 = Pow2In(0f, 1f, num2);
			if (_glow != null && GodotObject.IsInstanceValid(_glow))
			{
				_glow.Scale = Vector2.One * (0.5f + 1.7f * num3) * (1f + 0.09f * MathF.Sin(_scaledTime * 26f));
				_glow.Modulate = new Color(ChargeColor, 0.8f * num3);
			}
			for (int i = 0; i < 12; i++)
			{
				Sprite2D sprite2D = _motes[i];
				if (sprite2D != null && GodotObject.IsInstanceValid(sprite2D))
				{
					float num4 = (_motePhase[i] + _realTime * 0.32f) % 1f;
					float num5 = Lerp(200f, 8f, num4);
					float x = _moteAngle[i] + num4 * 1.4f;
					sprite2D.Position = new Vector2(MathF.Cos(x), MathF.Sin(x)) * num5;
					sprite2D.Modulate = new Color(ChargeColor, num3 * (1f - MathF.Abs(num4 - 0.5f) * 1.6f));
				}
			}
			float num6 = Clamp01(num2 / Math.Max(0.01f, 0.7f));
			int num7 = 1 + (int)(num6 * 6f);
			for (int j = 0; j < num7; j++)
			{
				if (!_lit[j])
				{
					_lit[j] = true;
					_born[j] = _realTime;
				}
			}
			for (int k = 0; k < 7; k++)
			{
				if (!_lit[k])
				{
					SetVisible(k, on: false);
					continue;
				}
				float num8 = Pow2Out(0f, 1f, Clamp01((_realTime - _born[k]) / _growSeconds));
				if (num8 <= 0.02f)
				{
					SetVisible(k, on: false);
					continue;
				}
				SetVisible(k, on: true);
				Vector2[] points = Creep(_path[k], num8);
				StsLightningBolt.SetPoints(_outer[k], points);
				StsLightningBolt.SetPoints(_mid[k], points);
				StsLightningBolt.SetPoints(_core[k], points);
				Curve taper = _taper[k];
				float num9 = (0.25f + 0.75f * num3) * (0.3f + 0.7f * num8);
				Apply(_outer[k], 62f * num9, taper, (0.3f + 0.25f * num3) * num8);
				Apply(_mid[k], 36f * num9, taper, (0.5f + 0.35f * num3) * num8);
				Apply(_core[k], 13f * num9, taper, (0.7f + 0.3f * num3) * num8);
				bool flag = num8 > 0.55f && _forkPath[k] != null;
				SetForkVisible(k, flag);
				if (flag)
				{
					float num10 = Clamp01((num8 - 0.55f) / 0.45f);
					Vector2[] points2 = Creep(_forkPath[k], num10);
					Curve taper2 = _forkTaper[k];
					StsLightningBolt.SetPoints(_forkOuter[k], points2);
					StsLightningBolt.SetPoints(_forkCore[k], points2);
					Apply(_forkOuter[k], 28f * num9, taper2, (0.25f + 0.2f * num3) * num10);
					Apply(_forkCore[k], 9f * num9, taper2, (0.6f + 0.3f * num3) * num10);
				}
			}
			return true;
		}

		private static Vector2[] Creep(Vector2[] path, float fraction)
		{
			if (path.Length < 2)
			{
				return path;
			}
			fraction = Clamp01(fraction);
			if (fraction >= 0.999f)
			{
				return path;
			}
			float num = 0f;
			for (int i = 1; i < path.Length; i++)
			{
				num += path[i].DistanceTo(path[i - 1]);
			}
			float num2 = num * fraction;
			if (num2 <= 0.001f)
			{
				return new Vector2[2]
				{
					path[0],
					path[0]
				};
			}
			List<Vector2> list = new List<Vector2> { path[0] };
			float num3 = 0f;
			for (int j = 1; j < path.Length; j++)
			{
				float num4 = path[j].DistanceTo(path[j - 1]);
				if (num3 + num4 >= num2)
				{
					float weight = ((num4 <= 0.001f) ? 1f : ((num2 - num3) / num4));
					list.Add(path[j - 1].Lerp(path[j], weight));
					break;
				}
				num3 += num4;
				list.Add(path[j]);
			}
			return list.ToArray();
		}

		private void SetForkVisible(int i, bool on)
		{
			Line2D[][] array = new Line2D[2][] { _forkOuter, _forkCore };
			for (int j = 0; j < array.Length; j++)
			{
				Line2D line2D = array[j][i];
				if (line2D != null && GodotObject.IsInstanceValid(line2D))
				{
					line2D.Visible = on;
				}
			}
		}

		private static void Apply(Line2D? line, float width, Curve? taper, float alpha)
		{
			if (line != null && GodotObject.IsInstanceValid(line))
			{
				line.Width = Math.Max(1f, width);
				if (taper != null)
				{
					line.WidthCurve = taper;
				}
				line.Modulate = new Color(1f, 1f, 1f, Clamp01(alpha));
			}
		}

		private void SetVisible(int i, bool on)
		{
			Line2D[][] array = new Line2D[5][] { _outer, _mid, _core, _forkOuter, _forkCore };
			for (int j = 0; j < array.Length; j++)
			{
				Line2D line2D = array[j][i];
				if (line2D != null && GodotObject.IsInstanceValid(line2D))
				{
					line2D.Visible = on;
				}
			}
		}
	}

	private sealed class StsStaffTrailEffect : VfxAnimator
	{
		private const int MaxPoints = 16;

		private readonly Creature _caster;

		private readonly double _life;

		private readonly double _start;

		private readonly List<Vector2> _history = new List<Vector2>();

		private readonly Line2D? _glow;

		private readonly Line2D? _core;

		internal StsStaffTrailEffect(Creature caster, double life)
		{
			_caster = caster;
			_life = life;
			_start = (double)Time.GetTicksMsec() / 1000.0;
			if (MountRoot(Vector2.Zero))
			{
				Curve curve = new Curve
				{
					MinValue = 0f,
					MaxValue = 1f
				};
				curve.AddPoint(new Vector2(0f, 0.05f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
				curve.AddPoint(new Vector2(0.55f, 0.42f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
				curve.AddPoint(new Vector2(1f, 1f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
				_glow = MakeTrailLine(34f, new Color(BoltOuter, 0.22f), curve);
				_core = MakeTrailLine(10f, new Color(ChargeColor, 0.5f), curve);
				if (_glow == null && _core == null)
				{
					Failed = true;
				}
			}
		}

		private Line2D? MakeTrailLine(float width, Color color, Curve taper)
		{
			Line2D line2D = new Line2D
			{
				Width = width,
				DefaultColor = color,
				WidthCurve = taper,
				JointMode = Line2D.LineJointMode.Round,
				BeginCapMode = Line2D.LineCapMode.Round,
				EndCapMode = Line2D.LineCapMode.Round,
				Antialiased = true,
				Material = new CanvasItemMaterial
				{
					BlendMode = CanvasItemMaterial.BlendModeEnum.Add
				}
			};
			Root.AddChild(line2D, forceReadableName: false, Node.InternalMode.Disabled);
			return line2D;
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			double num = (double)Time.GetTicksMsec() / 1000.0 - _start;
			Vector2 vector = StaffEyePosition(_caster);
			if (_history.Count != 0)
			{
				List<Vector2> history = _history;
				if (!(history[history.Count - 1].DistanceSquaredTo(vector) > 9f))
				{
					List<Vector2> history2 = _history;
					history2[history2.Count - 1] = vector;
					if (_history.Count > 1)
					{
						_history.RemoveAt(0);
					}
					goto IL_00aa;
				}
			}
			_history.Add(vector);
			goto IL_00aa;
			IL_00aa:
			while (_history.Count > 16)
			{
				_history.RemoveAt(0);
			}
			if (num >= _life)
			{
				if (_history.Count <= 2)
				{
					return false;
				}
				_history.RemoveAt(0);
			}
			float num2 = 0f;
			for (int i = 1; i < _history.Count; i++)
			{
				num2 += _history[i].DistanceTo(_history[i - 1]);
			}
			bool visible = num2 > 24f;
			Vector2[] points = _history.ToArray();
			StsLightningBolt.SetPoints(_glow, points);
			StsLightningBolt.SetPoints(_core, points);
			if (_glow != null && GodotObject.IsInstanceValid(_glow))
			{
				_glow.Visible = visible;
			}
			if (_core != null && GodotObject.IsInstanceValid(_core))
			{
				_core.Visible = visible;
			}
			float a = ((num < _life) ? 1f : Clamp01((float)_history.Count / 16f));
			Root.Modulate = new Color(1f, 1f, 1f, a);
			return true;
		}
	}

	private sealed class StsShockRingEffect : VfxAnimator
	{
		private const float Life = 0.34f;

		private readonly Sprite2D? _ring;

		private readonly Sprite2D? _inner;

		private float _time;

		internal StsShockRingEffect(Vector2 origin, Vector2 dir)
		{
			float rotationDeg = Mathf.RadToDeg(dir.Angle());
			if (MountRoot(origin, rotationDeg))
			{
				Texture2D texture = StsAssets.Region("combat/empowerCircle1") ?? StsAssets.Region("shine1");
				_ring = Sprite(texture, Vector2.Zero, Colors.White, 0f, additive: true);
				_inner = Sprite(texture, Vector2.Zero, new Color(BoltOuter, 0.8f), 0f, additive: true);
				if (_inner != null)
				{
					Root.AddChild(_inner, forceReadableName: false, Node.InternalMode.Disabled);
				}
				if (_ring != null)
				{
					Root.AddChild(_ring, forceReadableName: false, Node.InternalMode.Disabled);
				}
				if (_ring == null && _inner == null)
				{
					Failed = true;
				}
			}
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			_time += delta;
			if (_time >= 0.34f)
			{
				return false;
			}
			float num = _time / 0.34f;
			float num2 = Exp5In(0f, 1f, 1f - num);
			if (_ring != null)
			{
				_ring.Scale = new Vector2(Pow2Out(0.4f, 2.2f, num), Pow2Out(0.8f, 8.5f, num));
				_ring.Modulate = new Color(1f, 1f, 1f, num2 * 0.85f);
			}
			if (_inner != null)
			{
				_inner.Scale = new Vector2(Pow2Out(0.3f, 3.6f, num), Pow2Out(0.5f, 5.2f, num));
				_inner.Modulate = new Color(BoltOuter, num2 * 0.55f);
			}
			return true;
		}
	}

	private sealed class StsStaffFlashEffect : VfxAnimator
	{
		private const float Life = 0.28f;

		private readonly Creature _caster;

		private readonly Sprite2D? _flash;

		private readonly Sprite2D? _ring;

		private float _time;

		internal StsStaffFlashEffect(Creature caster)
		{
			_caster = caster;
			if (MountRoot(StaffEyePosition(caster)))
			{
				Texture2D texture = StsAssets.Region("combat/empowerCircle1") ?? StsAssets.Region("shine1");
				_flash = Sprite(texture, Vector2.Zero, Colors.White, 0f, additive: true);
				_ring = Sprite(texture, Vector2.Zero, new Color(BoltOuter, 0.9f), 0f, additive: true);
				if (_ring != null)
				{
					Root.AddChild(_ring, forceReadableName: false, Node.InternalMode.Disabled);
				}
				if (_flash != null)
				{
					Root.AddChild(_flash, forceReadableName: false, Node.InternalMode.Disabled);
				}
				if (_flash == null && _ring == null)
				{
					Failed = true;
				}
			}
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			_time += delta;
			if (_time >= 0.28f)
			{
				return false;
			}
			Root.Position = StaffEyePosition(_caster);
			float num = _time / 0.28f;
			if (_flash != null)
			{
				_flash.Scale = Vector2.One * Pow2Out(1.2f, 4.2f, num);
				_flash.Modulate = new Color(1f, 1f, 1f, Exp5In(0f, 1f, 1f - num));
			}
			if (_ring != null)
			{
				_ring.Scale = Vector2.One * Pow2Out(0.6f, 7.5f, num);
				_ring.Modulate = new Color(BoltOuter, Exp5In(0f, 0.7f, 1f - num));
			}
			return true;
		}
	}

	private sealed class StsStarBurstEffect : VfxAnimator
	{
		private const float Life = 0.45f;

		private const int Rays = 12;

		private readonly Sprite2D? _flare;

		private readonly Sprite2D?[] _rays = new Sprite2D[12];

		private float _time;

		internal StsStarBurstEffect(Vector2 p)
		{
			if (!MountRoot(p))
			{
				return;
			}
			Texture2D texture2D = StsAssets.Region("shine1") ?? StsAssets.Region("combat/tinyStar2");
			Texture2D texture = StsAssets.Region("combat/strikeLine2") ?? texture2D;
			for (int i = 0; i < 12; i++)
			{
				float rotationDeg = (float)i * 30f + R(-8f, 8f);
				Sprite2D sprite2D = Sprite(texture, Vector2.Zero, new Color(BurstGold, 0.9f), rotationDeg, additive: true);
				if (sprite2D != null)
				{
					Root.AddChild(sprite2D, forceReadableName: false, Node.InternalMode.Disabled);
					_rays[i] = sprite2D;
				}
			}
			_flare = Sprite(texture2D, Vector2.Zero, Colors.White, R(0f, 360f), additive: true);
			if (_flare != null)
			{
				Root.AddChild(_flare, forceReadableName: false, Node.InternalMode.Disabled);
			}
			if (_flare == null && _rays[0] == null)
			{
				Failed = true;
			}
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			_time += delta;
			if (_time >= 0.45f)
			{
				return false;
			}
			float num = _time / 0.45f;
			if (_flare != null)
			{
				_flare.Scale = Vector2.One * Pow2Out(0.8f, 3.6f, num);
				_flare.RotationDegrees += 160f * delta;
				_flare.Modulate = new Color(1f, 0.95f, 0.8f, Exp5In(0f, 1f, 1f - num));
			}
			for (int i = 0; i < 12; i++)
			{
				Sprite2D sprite2D = _rays[i];
				if (sprite2D != null && GodotObject.IsInstanceValid(sprite2D))
				{
					float num2 = Pow2Out(0.4f, 3.2f + (float)(i % 3) * 0.7f, num);
					sprite2D.Scale = new Vector2(num2, 0.55f);
					float x = Mathf.DegToRad(sprite2D.RotationDegrees);
					sprite2D.Position = new Vector2(MathF.Cos(x), MathF.Sin(x)) * (num2 * 26f);
					sprite2D.Modulate = new Color(BurstGold, Exp5In(0f, 0.9f, 1f - num));
				}
			}
			return true;
		}
	}

	private static class WaveCrescent
	{
		internal const int Samples = 48;

		internal const float ArcRadius = 520f;

		internal const float ArcHalfAngle = 1.12f;

		internal static readonly float[] LayerWidths = new float[5] { 460f, 270f, 160f, 74f, 26f };

		internal static readonly Color[] LayerColors = new Color[5]
		{
			BoltOuter,
			BoltOuter,
			new Color(0.86f, 0.3f, 1f),
			BoltMid,
			BoltCore
		};

		internal static readonly float[] LayerAlphas = new float[5] { 0.08f, 0.22f, 0.42f, 0.74f, 1f };

		internal static Curve BuildTaper()
		{
			Curve curve = new Curve();
			curve.MinValue = 0f;
			curve.MaxValue = 1f;
			curve.AddPoint(new Vector2(0f, 0.06f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			curve.AddPoint(new Vector2(0.18f, 0.62f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			curve.AddPoint(new Vector2(0.5f, 1f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			curve.AddPoint(new Vector2(0.82f, 0.62f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			curve.AddPoint(new Vector2(1f, 0.06f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			return curve;
		}

		internal static Line2D MakeLine(Node2D root, float width, Color color, Curve taper)
		{
			Line2D line2D = new Line2D
			{
				Width = Math.Max(1f, width),
				DefaultColor = new Color(color, 0f),
				WidthCurve = taper,
				JointMode = Line2D.LineJointMode.Round,
				BeginCapMode = Line2D.LineCapMode.Round,
				EndCapMode = Line2D.LineCapMode.Round,
				Antialiased = true,
				Material = new CanvasItemMaterial
				{
					BlendMode = CanvasItemMaterial.BlendModeEnum.Add
				}
			};
			root.AddChild(line2D, forceReadableName: false, Node.InternalMode.Disabled);
			return line2D;
		}

		internal static void Lay(Vector2[] points, Vector2 front, Vector2 dir, float radius)
		{
			float num = dir.Angle();
			Vector2 vector = front - dir * radius;
			for (int i = 0; i < points.Length; i++)
			{
				float weight = (float)i / ((float)points.Length - 1f);
				float angle = num + Mathf.Lerp(-1.12f, 1.12f, weight);
				points[i] = vector + Vector2.FromAngle(angle) * radius;
			}
		}

		internal static void Style(Line2D?[] lines, float scale, float alpha, float widthMul)
		{
			for (int i = 0; i < lines.Length; i++)
			{
				Line2D line2D = lines[i];
				if (line2D != null && GodotObject.IsInstanceValid(line2D))
				{
					line2D.Width = Math.Max(1f, LayerWidths[i] * scale * widthMul);
					line2D.DefaultColor = new Color(LayerColors[i], LayerAlphas[i] * alpha);
				}
			}
		}
	}

	private sealed class StsWaveProjectileEffect : VfxAnimator
	{
		private const float HoldSeconds = 0.05f;

		private const float FadeSeconds = 0.26f;

		private const float Overshoot = 220f;

		private const float LaunchScale = 0.35f;

		private const float ArrivalScale = 1.15f;

		private const float GhostSpacing = 70f;

		private static readonly float[] RippleBack = new float[4] { 0f, 95f, 190f, 285f };

		private static readonly float[] RippleScale = new float[4] { 1f, 0.9f, 0.8f, 0.7f };

		private static readonly float[] RippleAlpha = new float[4] { 1f, 0.62f, 0.42f, 0.26f };

		private readonly Vector2 _from;

		private readonly Vector2 _to;

		private readonly Vector2 _dir;

		private readonly float _travel;

		private readonly Line2D?[][] _ripples = new Line2D[RippleBack.Length][];

		private readonly Line2D? _wakeHaze;

		private readonly Line2D? _wakeCore;

		private readonly Vector2[] _points = new Vector2[48];

		private readonly Vector2[] _wakePoints = new Vector2[2];

		private float _time;

		private float _ghostDistance;

		internal StsWaveProjectileEffect(Vector2 from, Vector2 to, float travelSeconds)
		{
			_from = from;
			_to = to;
			Vector2 vector = to - from;
			_dir = ((vector.LengthSquared() < 1f) ? Vector2.Right : vector.Normalized());
			_travel = Math.Max(0.05f, travelSeconds);
			if (!MountRoot(Vector2.Zero))
			{
				return;
			}
			Curve taper = WaveCrescent.BuildTaper();
			Curve curve = new Curve
			{
				MinValue = 0f,
				MaxValue = 1f
			};
			curve.AddPoint(new Vector2(0f, 0.05f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			curve.AddPoint(new Vector2(0.55f, 0.5f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			curve.AddPoint(new Vector2(1f, 1f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			_wakeHaze = WaveCrescent.MakeLine(Root, 300f, BoltOuter, curve);
			_wakeCore = WaveCrescent.MakeLine(Root, 120f, BoltMid, curve);
			bool flag = false;
			for (int num = RippleBack.Length - 1; num >= 0; num--)
			{
				Line2D[] array = new Line2D[WaveCrescent.LayerWidths.Length];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = WaveCrescent.MakeLine(Root, WaveCrescent.LayerWidths[i] * RippleScale[num], WaveCrescent.LayerColors[i], taper);
					flag |= array[i] != null;
				}
				_ripples[num] = array;
			}
			if (!flag)
			{
				Failed = true;
			}
			else
			{
				Rebuild(_from, 0.35f, 0f, 1f);
			}
		}

		private void Rebuild(Vector2 front, float scale, float alpha, float widthMul)
		{
			for (int i = 0; i < RippleBack.Length; i++)
			{
				float radius = 520f * scale * RippleScale[i];
				Vector2 front2 = front - _dir * (RippleBack[i] * scale);
				WaveCrescent.Lay(_points, front2, _dir, radius);
				Line2D[] array = _ripples[i];
				for (int j = 0; j < array.Length; j++)
				{
					StsLightningBolt.SetPoints(array[j], _points);
				}
				WaveCrescent.Style(array, scale * RippleScale[i], alpha * RippleAlpha[i], widthMul);
			}
			_wakePoints[0] = _from;
			_wakePoints[1] = front - _dir * (40f * scale);
			float num = alpha * 0.9f;
			if (_wakeHaze != null && GodotObject.IsInstanceValid(_wakeHaze))
			{
				StsLightningBolt.SetPoints(_wakeHaze, _wakePoints);
				_wakeHaze.Width = Math.Max(1f, 300f * scale * widthMul);
				_wakeHaze.DefaultColor = new Color(BoltOuter, 0.1f * num);
			}
			if (_wakeCore != null && GodotObject.IsInstanceValid(_wakeCore))
			{
				StsLightningBolt.SetPoints(_wakeCore, _wakePoints);
				_wakeCore.Width = Math.Max(1f, 120f * scale * widthMul);
				_wakeCore.DefaultColor = new Color(BoltMid, 0.28f * num);
			}
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			_time += delta;
			float widthMul = 1f;
			Vector2 vector;
			float scale;
			float alpha;
			if (_time < _travel)
			{
				float num = Clamp01(_time / _travel);
				float num2 = 1f - (1f - num) * (1f - num) * (1f - num) * 0.5f - 0.5f * (1f - num);
				vector = _from + (_to - _from) * num2;
				float t = Clamp01(num / 0.33f);
				scale = Lerp(0.35f, 1f, t) * Lerp(1f, 1.15f, num);
				alpha = Lerp(0.55f, 1f, Clamp01(num / 0.2f));
				float num3 = _from.DistanceTo(vector);
				while (num3 - _ghostDistance >= 70f)
				{
					_ghostDistance += 70f;
					Vector2 at = _from + _dir * _ghostDistance;
					float t2 = Clamp01(_ghostDistance / Math.Max(1f, _from.DistanceTo(_to)));
					Emit(new StsWaveGhostEffect(scale: Lerp(0.35f, 1.15f, t2) * 0.92f, at: at, dir: _dir, alpha: Lerp(0.22f, 0.4f, t2), life: 0.18f));
				}
			}
			else if (_time < _travel + 0.05f)
			{
				vector = _to;
				scale = 1.15f * (1f + 0.04f * Mathf.Sin((_time - _travel) * 90f));
				alpha = 1f;
			}
			else
			{
				float num4 = Clamp01((_time - _travel - 0.05f) / 0.26f);
				vector = _to + _dir * (220f * num4);
				scale = 1.15f * (1f + 0.7f * num4);
				alpha = (1f - num4) * (1f - num4);
				widthMul = Lerp(1f, 0.3f, num4);
			}
			Rebuild(vector, scale, alpha, widthMul);
			return _time < _travel + 0.05f + 0.26f;
		}
	}

	private sealed class StsWaveGhostEffect : VfxAnimator
	{
		private readonly Vector2 _at;

		private readonly Vector2 _dir;

		private readonly float _scale;

		private readonly float _alpha;

		private readonly float _life;

		private readonly Line2D?[] _lines = new Line2D[WaveCrescent.LayerWidths.Length];

		private readonly Vector2[] _points = new Vector2[48];

		private float _time;

		internal StsWaveGhostEffect(Vector2 at, Vector2 dir, float scale, float alpha, float life)
		{
			_at = at;
			_dir = dir;
			_scale = scale;
			_alpha = alpha;
			_life = Math.Max(0.02f, life);
			if (MountRoot(Vector2.Zero))
			{
				Curve taper = WaveCrescent.BuildTaper();
				bool flag = false;
				for (int i = 0; i < _lines.Length; i++)
				{
					_lines[i] = WaveCrescent.MakeLine(Root, WaveCrescent.LayerWidths[i] * scale, WaveCrescent.LayerColors[i], taper);
					flag |= _lines[i] != null;
				}
				if (!flag)
				{
					Failed = true;
				}
				else
				{
					Lay(0f);
				}
			}
		}

		private void Lay(float drift)
		{
			WaveCrescent.Lay(_points, _at + _dir * drift, _dir, 520f * _scale);
			Line2D[] lines = _lines;
			for (int i = 0; i < lines.Length; i++)
			{
				StsLightningBolt.SetPoints(lines[i], _points);
			}
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			_time += delta;
			float num = Clamp01(_time / _life);
			Lay(28f * num);
			WaveCrescent.Style(_lines, _scale, _alpha * (1f - num) * (1f - num), Lerp(1f, 0.5f, num));
			return _time < _life;
		}
	}

	private sealed class StsStaffAfterimageEffect : VfxAnimator
	{
		private const int Samples = 56;

		private const float HoldSeconds = 0.07f;

		private static readonly float[] LayerWidths = new float[6] { 430f, 250f, 158f, 92f, 44f, 15f };

		private static readonly Color[] LayerColors = new Color[6]
		{
			BoltOuter,
			BoltOuter,
			new Color(0.86f, 0.3f, 1f),
			BoltMid,
			new Color(0.92f, 0.78f, 1f),
			BoltCore
		};

		private static readonly float[] LayerAlphas = new float[6] { 0.08f, 0.2f, 0.34f, 0.6f, 0.85f, 1f };

		private static readonly float[] LayerCollapse = new float[6] { 0.02f, 0.04f, 0.08f, 0.16f, 0.35f, 0.62f };

		private readonly Vector2 _pivot;

		private readonly float _startAngle;

		private readonly float _endAngle;

		private readonly float _sweep;

		private readonly float _ahead;

		private readonly float _back;

		private readonly float _bow;

		private readonly float _reachStart;

		private readonly float _reachEnd;

		private readonly float _widthScale;

		private readonly float _alphaScale;

		private readonly float _life;

		private readonly float _delay;

		private readonly Line2D?[] _layers = new Line2D[LayerWidths.Length];

		private readonly Vector2[] _points = new Vector2[56];

		private float _time;

		internal StsStaffAfterimageEffect(Vector2 pivot, float startAngle, float endAngle, float sweepSeconds, float ahead, float back, float bow, float reachStart, float reachEnd, float widthScale, float alphaScale, float life, float delay)
		{
			_pivot = pivot;
			_startAngle = startAngle;
			_endAngle = endAngle;
			_sweep = Math.Max(0f, sweepSeconds);
			_ahead = ahead;
			_back = back;
			_bow = bow;
			_reachStart = reachStart;
			_reachEnd = reachEnd;
			_widthScale = widthScale;
			_alphaScale = alphaScale;
			_life = life;
			_delay = Math.Max(0f, delay);
			if (MountRoot(Vector2.Zero))
			{
				Curve taper = BuildSmearCurve();
				bool flag = false;
				for (int i = 0; i < LayerWidths.Length; i++)
				{
					_layers[i] = MakeSmearLine(LayerWidths[i] * widthScale, LayerColors[i], taper);
					flag |= _layers[i] != null;
				}
				if (!flag)
				{
					Failed = true;
					return;
				}
				Rebuild(_reachStart, _startAngle);
				SetLayers(1f, 0f, 0f);
			}
		}

		private static Curve BuildSmearCurve()
		{
			Curve curve = new Curve();
			curve.MinValue = 0f;
			curve.MaxValue = 1f;
			curve.AddPoint(new Vector2(0f, 0f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			curve.AddPoint(new Vector2(0.07f, 0.42f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			curve.AddPoint(new Vector2(0.2f, 0.86f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			curve.AddPoint(new Vector2(0.34f, 1f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			curve.AddPoint(new Vector2(0.6f, 0.9f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			curve.AddPoint(new Vector2(0.82f, 0.52f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			curve.AddPoint(new Vector2(1f, 0f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
			return curve;
		}

		private Line2D MakeSmearLine(float width, Color color, Curve taper)
		{
			Line2D line2D = new Line2D
			{
				Width = Math.Max(1f, width),
				DefaultColor = color,
				WidthCurve = taper,
				JointMode = Line2D.LineJointMode.Round,
				BeginCapMode = Line2D.LineCapMode.Round,
				EndCapMode = Line2D.LineCapMode.Round,
				Antialiased = true,
				Material = new CanvasItemMaterial
				{
					BlendMode = CanvasItemMaterial.BlendModeEnum.Add
				}
			};
			Root.AddChild(line2D, forceReadableName: false, Node.InternalMode.Disabled);
			return line2D;
		}

		private void Rebuild(float frac, float angle)
		{
			frac = Math.Max(0.02f, frac);
			Vector2 vector = Vector2.FromAngle(angle);
			Vector2 vector2 = new Vector2(0f - vector.Y, vector.X);
			Vector2 vector3 = _pivot + vector * (_ahead * frac);
			Vector2 vector4 = _pivot - vector * (_back * Math.Min(1f, frac * 2.5f));
			Vector2 vector5 = vector3 - vector4;
			for (int i = 0; i < 56; i++)
			{
				float num = (float)i / 55f;
				_points[i] = vector4 + vector5 * num + vector2 * (_bow * Mathf.Sin(num * (float)Math.PI));
			}
			Line2D[] layers = _layers;
			for (int j = 0; j < layers.Length; j++)
			{
				StsLightningBolt.SetPoints(layers[j], _points);
			}
		}

		private void SetLayers(float widthMul, float alpha, float decay)
		{
			for (int i = 0; i < _layers.Length; i++)
			{
				Line2D line2D = _layers[i];
				if (line2D != null && GodotObject.IsInstanceValid(line2D))
				{
					float num = ((decay <= 0f) ? widthMul : (widthMul * Lerp(1f, LayerCollapse[i], decay * decay)));
					line2D.Width = Math.Max(1f, LayerWidths[i] * _widthScale * num);
					line2D.DefaultColor = new Color(LayerColors[i], LayerAlphas[i] * alpha * _alphaScale);
				}
			}
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			_time += delta;
			float num = _time - _delay;
			if (num < 0f)
			{
				return true;
			}
			float num2 = _sweep + 0.07f;
			float num3 = 0f;
			float widthMul;
			float alpha;
			if (num < _sweep)
			{
				float num4 = Clamp01(num / _sweep);
				float num5 = 1f - (1f - num4) * (1f - num4);
				Rebuild(Lerp(_reachStart, _reachEnd, num5), Mathf.Lerp(_startAngle, _endAngle, num5));
				widthMul = 0.78f + 0.22f * num5;
				alpha = 1f;
			}
			else if (num < num2)
			{
				Rebuild(_reachEnd, _endAngle);
				widthMul = 1f + 0.05f * Mathf.Sin((num - _sweep) * 70f);
				alpha = 1f;
			}
			else
			{
				num3 = Clamp01((num - num2) / Math.Max(0.001f, _life - num2));
				Rebuild(_reachEnd, _endAngle);
				widthMul = 1f;
				alpha = (1f - num3) * (1f - num3);
				Root.Position = Vector2.FromAngle(_endAngle) * Lerp(0f, 34f, num3);
			}
			SetLayers(widthMul, alpha, num3);
			return num < _life;
		}
	}

	private sealed class StsSwingArcEffect : VfxAnimator
	{
		private const int Samples = 44;

		private const float FadeSeconds = 0.24f;

		private readonly Vector2 _pivot;

		private readonly float _startAngle;

		private readonly float _endAngle;

		private readonly float _radius;

		private readonly float _sweep;

		private readonly Line2D? _bloom;

		private readonly Line2D? _core;

		private readonly Vector2[] _points = new Vector2[44];

		private float _time;

		internal StsSwingArcEffect(Vector2 pivot, float startAngle, float endAngle, float radius, float sweepSeconds)
		{
			_pivot = pivot;
			_startAngle = startAngle;
			_endAngle = endAngle;
			_radius = radius;
			_sweep = Math.Max(0.01f, sweepSeconds);
			if (MountRoot(Vector2.Zero))
			{
				Curve curve = new Curve
				{
					MinValue = 0f,
					MaxValue = 1f
				};
				curve.AddPoint(new Vector2(0f, 0.05f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
				curve.AddPoint(new Vector2(0.45f, 0.4f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
				curve.AddPoint(new Vector2(0.86f, 1f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
				curve.AddPoint(new Vector2(1f, 0.55f), 0f, 0f, Curve.TangentMode.Free, Curve.TangentMode.Free);
				_bloom = MakeArcLine(96f, BoltOuter, curve);
				_core = MakeArcLine(26f, BoltCore, curve);
				if (_bloom == null && _core == null)
				{
					Failed = true;
				}
			}
		}

		private Line2D MakeArcLine(float width, Color color, Curve taper)
		{
			Line2D line2D = new Line2D
			{
				Width = width,
				DefaultColor = new Color(color, 0f),
				WidthCurve = taper,
				JointMode = Line2D.LineJointMode.Round,
				BeginCapMode = Line2D.LineCapMode.Round,
				EndCapMode = Line2D.LineCapMode.Round,
				Antialiased = true,
				Material = new CanvasItemMaterial
				{
					BlendMode = CanvasItemMaterial.BlendModeEnum.Add
				}
			};
			Root.AddChild(line2D, forceReadableName: false, Node.InternalMode.Disabled);
			return line2D;
		}

		private void Rebuild(float progress, float radius)
		{
			float to = Mathf.Lerp(_startAngle, _endAngle, Clamp01(progress));
			for (int i = 0; i < 44; i++)
			{
				float weight = (float)i / 43f;
				float angle = Mathf.Lerp(_startAngle, to, weight);
				_points[i] = _pivot + Vector2.FromAngle(angle) * radius;
			}
			StsLightningBolt.SetPoints(_bloom, _points);
			StsLightningBolt.SetPoints(_core, _points);
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead)
			{
				return false;
			}
			_time += delta;
			float radius = _radius;
			float num2;
			float num3;
			if (_time < _sweep)
			{
				float num = Clamp01(_time / _sweep);
				Rebuild(1f - (1f - num) * (1f - num), radius);
				num2 = 0.9f;
				num3 = 1f;
			}
			else
			{
				float num4 = Clamp01((_time - _sweep) / 0.24f);
				radius = _radius * (1f + 0.22f * num4);
				Rebuild(1f, radius);
				num2 = 0.9f * (1f - num4) * (1f - num4);
				num3 = Lerp(1f, 0.25f, num4);
			}
			if (_bloom != null && GodotObject.IsInstanceValid(_bloom))
			{
				_bloom.Width = Math.Max(1f, 96f * num3);
				_bloom.DefaultColor = new Color(BoltOuter, 0.3f * num2);
			}
			if (_core != null && GodotObject.IsInstanceValid(_core))
			{
				_core.Width = Math.Max(1f, 26f * num3);
				_core.DefaultColor = new Color(BoltCore, 0.85f * num2);
			}
			return _time < _sweep + 0.24f;
		}
	}

	private sealed class StsBeamShardEffect : VfxAnimator
	{
		private readonly Vector2 _velocity;

		private readonly float _life;

		private readonly Color _color;

		private readonly Sprite2D? _sprite;

		private float _time;

		internal StsBeamShardEffect(Vector2 p, Vector2 velocity, Color color, float life, float scale)
		{
			_velocity = velocity;
			_life = life;
			_color = color;
			if (MountRoot(p, Mathf.RadToDeg(velocity.Angle())))
			{
				_sprite = Sprite(StsAssets.Region("combat/strikeLine2") ?? StsAssets.Region("shine1"), Vector2.Zero, color, 0f, additive: true);
				if (_sprite == null)
				{
					Failed = true;
					return;
				}
				_sprite.Scale = new Vector2(scale * R(0.9f, 1.8f), scale * R(0.2f, 0.45f));
				Root.AddChild(_sprite, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}

		public override bool Advance(float delta)
		{
			if (base.RootDead || _sprite == null || !GodotObject.IsInstanceValid(_sprite))
			{
				return false;
			}
			_time += delta;
			float num = Clamp01(_time / _life);
			Root.Position += _velocity * delta * (1f - num * 0.7f);
			_sprite.Modulate = new Color(_color, 1f - num * num);
			_sprite.Scale = new Vector2(_sprite.Scale.X * (1f + delta * 1.6f), Math.Max(0.02f, _sprite.Scale.Y * (1f - delta * 0.9f)));
			return _time < _life;
		}
	}

	private const float StsScale = 1f;

	private static int _eyeDiagBudget = 16;

	internal const string ExpungeSlashAnim = "ExpungeSlash";

	private static readonly Color BladeCore = new Color(1f, 1f, 1f);

	private static readonly Color BladeMid = new Color(1f, 0.65f, 1f);

	private static readonly Color BladeGlow = new Color(1f, 0.16f, 0.85f);

	internal const float JudgmentLeadSeconds = 0.06f;

	internal const float JudgmentFallSeconds = 0.36f;

	internal const float JudgmentImpactDelay = 0.42000002f;

	internal const float JudgmentKillDelay = 0.52000004f;

	private const float JudgmentHoldSeconds = 0.2f;

	private const float JudgmentLiftSeconds = 0.3f;

	private const string JudgmentFallSfx = "res://audio/combat/judgment_fall.ogg";

	private const string JudgmentImpactSfx = "res://audio/combat/judgment_impact.ogg";

	private const string JudgmentIronSfx = "res://audio/combat/expunge_iron.ogg";

	private const string JudgmentTitleFontPath = "res://themes/kreon_bold_shared.tres";

	private static readonly Color JudgeGold = new Color(1f, 0.86f, 0.42f);

	private static readonly Color JudgeViolet = new Color(0.62f, 0.3f, 1f);

	private static readonly Color BoltCore = new Color(1f, 0.97f, 1f);

	private static readonly Color BoltMid = new Color(0.78f, 0.36f, 1f);

	private static readonly Color BoltOuter = new Color(0.95f, 0.24f, 0.9f);

	private static readonly Color ChargeColor = new Color(0.72f, 0.3f, 0.98f);

	private static readonly Color BurstGold = new Color(1f, 0.85f, 0.35f);

	internal const string SignatureSwingAnim = "SignatureMove";

	internal const float WaveTravelSeconds = 0.26f;

	private const float AfterimageOvershoot = 1700f;

	private const float AfterimageTail = 520f;

	internal const float AfterimageSweepSeconds = 0.14f;

	private const float AfterimageStartReach = 0.5f;

	private const float AfterimageStartAngle = (float)Math.PI / 2f;

	internal const float SignatureSwingSpeed = 1f;

	internal const double SignatureLaunchTime = 0.17;

	internal const double SignatureContactDelay = 0.17;

	internal static async Task WaitSeconds(double seconds)
	{
		if (!(seconds <= 0.0) && !NonInteractiveMode.IsActive && Engine.GetMainLoop() is SceneTree sceneTree)
		{
			SceneTreeTimer sceneTreeTimer = sceneTree.CreateTimer(seconds);
			await sceneTreeTimer.ToSignal(sceneTreeTimer, SceneTreeTimer.SignalName.Timeout);
		}
	}

	internal static async Task WaitRealSeconds(double seconds)
	{
		if (!(seconds <= 0.0) && !NonInteractiveMode.IsActive && Engine.GetMainLoop() is SceneTree sceneTree)
		{
			SceneTreeTimer sceneTreeTimer = sceneTree.CreateTimer(seconds, processAlways: true, processInPhysics: false, ignoreTimeScale: true);
			await sceneTreeTimer.ToSignal(sceneTreeTimer, SceneTreeTimer.SignalName.Timeout);
		}
	}

	internal static void FireAndForget(Task task)
	{
		WatcherObserve(task);
	}

	private static async Task WatcherObserve(Task task)
	{
		try
		{
			await task;
		}
		catch
		{
		}
	}

	internal static bool TryAddToCombatVfx(Node node, bool behind = false)
	{
		if (NonInteractiveMode.IsActive)
		{
			node.QueueFree();
			return false;
		}
		NCombatRoom instance = NCombatRoom.Instance;
		if (instance == null)
		{
			node.QueueFree();
			return false;
		}
		Node node2 = (behind ? (instance.GetType().GetProperty("BackCombatVfxContainer")?.GetValue(instance) as Node) : (instance.GetType().GetProperty("CombatVfxContainer")?.GetValue(instance) as Node));
		if (node2 == null)
		{
			node2 = instance;
		}
		node2.AddChild(node, forceReadableName: false, Node.InternalMode.Disabled);
		return true;
	}

	internal static Vector2 CreatureCenter(Creature creature)
	{
		Node2D node2D = (NCombatRoom.Instance?.GetCreatureNode(creature))?.Visuals;
		if (node2D != null)
		{
			return node2D.GlobalPosition + new Vector2(0f, -95f);
		}
		return Vector2.Zero;
	}

	internal static Rect2 CreatureBounds(Creature creature)
	{
		Vector2 vector = CreatureCenter(creature);
		Rect2 result = new Rect2(vector - new Vector2(110f, 130f), new Vector2(220f, 260f));
		try
		{
			Control control = (NCombatRoom.Instance?.GetCreatureNode(creature))?.Hitbox;
			if (control == null || !GodotObject.IsInstanceValid(control) || control.Size.X < 8f || control.Size.Y < 8f)
			{
				return result;
			}
			return new Rect2(control.GlobalPosition, control.Size);
		}
		catch
		{
			return result;
		}
	}

	internal static bool IsInStance(Creature creature)
	{
		if (!WatcherCombatHelper.IsInStance<Wrath>(creature) && !WatcherCombatHelper.IsInStance<Calm>(creature) && !WatcherCombatHelper.IsInStance<Divinity>(creature))
		{
			return WatcherCombatHelper.IsInStance<Foreseen>(creature);
		}
		return true;
	}

	internal static void PlayCleave(bool reversed = false)
	{
		Emit(new StsCleaveEffect(reversed));
	}

	internal static void PlayFlyingSleevesSlash(Creature target, bool second)
	{
		Emit(new StsAnimatedSlashEffect(CreatureCenter(target) + new Vector2(0f, -30f), new Vector2(500f, second ? (-200f) : 200f), second ? 250f : 290f, 3f, new Color(0.5f, 0f, 1f), Colors.Pink));
	}

	internal static void PlayFlickCoin(Creature source, Creature target)
	{
		Emit(new StsFlickCoinEffect(CreatureCenter(source), CreatureCenter(target)));
	}

	internal static void PlayClash(Creature target)
	{
		Vector2 vector = CreatureCenter(target);
		Emit(new StsAnimatedSlashEffect(vector + new Vector2(0f, -30f), new Vector2(-500f, -500f), 135f, 4f, new Color(1f, 0.141f, 0f), Colors.Gold));
		Emit(new StsAnimatedSlashEffect(vector + new Vector2(0f, -30f), new Vector2(500f, -500f), 225f, 4f, Colors.SkyBlue, Colors.Cyan));
		for (int i = 0; i < 15; i++)
		{
			Emit(new StsSimpleSparkleEffect(vector + new Vector2(R(-40f, 40f), R(-40f, 40f))));
		}
	}

	internal static void PlayWallop(int damage, Creature target)
	{
		int num = Math.Min(Math.Max(damage, 0), 50);
		Vector2 p = CreatureCenter(target);
		for (int i = 0; i < num; i++)
		{
			Emit(new StsStarBounceEffect(p));
		}
	}

	internal static void PlayEmptyStance(Creature owner)
	{
		Emit(new StsEmptyStanceEffect(CreatureCenter(owner)));
	}

	internal static void PlayViolentAttack(Creature target, Color color)
	{
		Emit(new StsViolentAttackEffect(CreatureCenter(target), color));
	}

	internal static void PlayExpunge(Creature target)
	{
		Rect2 rect = CreatureBounds(target);
		Vector2 vector = new Vector2(1f, 1f).Normalized();
		float num = Math.Clamp(rect.Size.Length() * 1.35f, 380f, 900f);
		Vector2 position = rect.Position;
		Vector2 vector2 = position + vector * (num * 0.5f);
		Emit(new StsAnimatedSlashEffect(vector2 + new Vector2(64f, 64f), (vector2 - position) * 2f / 1f, 135f, num / 128f, new Color(0.5f, 0f, 1f), Colors.Magenta));
		WatcherAudioHelper.PlayOneShot("res://audio/combat/expunge_beam.ogg", 0.7f);
		WatcherAudioHelper.PlayOneShot("res://audio/combat/expunge_iron.ogg", 0.2f);
	}

	internal static void PlayDivinityStanceChange(Creature owner)
	{
		Vector2 p = CreatureCenter(owner);
		for (int i = 0; i < 20; i++)
		{
			Emit(new StsDivinityStanceChangeParticle(Colors.Pink, p));
		}
	}

	internal static void StartDivinityEye(Creature owner)
	{
		DivinityEyeDriver.Start(owner);
	}

	internal static void StopDivinityEye(Creature owner)
	{
		DivinityEyeDriver.Stop(owner);
	}

	internal static void StartDevaEyeSweep(Creature owner)
	{
		DivinityEyeDriver.StartSweep(owner);
	}

	internal static void StopDevaEyeSweep(Creature owner)
	{
		DivinityEyeDriver.StopSweep(owner);
	}

	internal static void PlayBlasphemyEyeOpen(Creature owner)
	{
		Emit(new StsBlasphemyEyeOpenEffect(CreatureCenter(owner) + new Vector2(0f, -275f)));
	}

	private static void Emit(VfxAnimator animator)
	{
		VfxDriver.Add(animator);
	}

	internal static void EyeDiag(string msg)
	{
		if (_eyeDiagBudget > 0)
		{
			_eyeDiagBudget--;
			Log.Info("[Watcher][EYEDIAG] " + msg);
		}
	}

	private static float R(float min, float max)
	{
		return (float)GD.RandRange(min, max);
	}

	private static int RI(int min, int maxInclusive)
	{
		return GD.RandRange(min, maxInclusive + 1);
	}

	private static bool RB(double chance = 0.5)
	{
		return (double)GD.Randf() < chance;
	}

	private static float Clamp01(float t)
	{
		return Math.Clamp(t, 0f, 1f);
	}

	private static float Lerp(float from, float to, float t)
	{
		return from + (to - from) * Clamp01(t);
	}

	private static float Linear(float a, float b, float t)
	{
		return Lerp(a, b, t);
	}

	private static float Fade(float a, float b, float t)
	{
		t = Clamp01(t);
		t = t * t * t * (t * (t * 6f - 15f) + 10f);
		return Lerp(a, b, t);
	}

	private static float Pow2In(float a, float b, float t)
	{
		t = Clamp01(t);
		return Lerp(a, b, t * t);
	}

	private static float Pow2Out(float a, float b, float t)
	{
		t = Clamp01(t);
		return Lerp(a, b, 1f - (1f - t) * (1f - t));
	}

	private static float Pow2(float a, float b, float t)
	{
		return Pow2In(a, b, t);
	}

	private static float Pow3In(float a, float b, float t)
	{
		t = Clamp01(t);
		return Lerp(a, b, t * t * t);
	}

	private static float Pow5In(float a, float b, float t)
	{
		t = Clamp01(t);
		return Lerp(a, b, MathF.Pow(t, 5f));
	}

	private static float Exp5In(float a, float b, float t)
	{
		t = Clamp01(t);
		if (t != 0f)
		{
			return Lerp(a, b, MathF.Pow(2f, 5f * (t - 1f)));
		}
		return a;
	}

	private static float Exp10In(float a, float b, float t)
	{
		t = Clamp01(t);
		if (t != 0f)
		{
			return Lerp(a, b, MathF.Pow(2f, 10f * (t - 1f)));
		}
		return a;
	}

	private static float CircleIn(float a, float b, float t)
	{
		t = Clamp01(t);
		return Lerp(a, b, 1f - MathF.Sqrt(1f - t * t));
	}

	private static float CircleOut(float a, float b, float t)
	{
		t = Clamp01(t) - 1f;
		return Lerp(a, b, MathF.Sqrt(1f - t * t));
	}

	private static Sprite2D? Sprite(Texture2D? texture, Vector2 position, Color color, float rotationDeg = 0f, bool additive = false)
	{
		if (texture == null)
		{
			return null;
		}
		Sprite2D sprite2D = new Sprite2D
		{
			Texture = texture,
			Position = position,
			Centered = true,
			Modulate = color,
			RotationDegrees = rotationDeg
		};
		if (additive)
		{
			sprite2D.Material = new CanvasItemMaterial
			{
				BlendMode = CanvasItemMaterial.BlendModeEnum.Add
			};
		}
		return sprite2D;
	}

	internal static bool HasExpungeSlash(Creature creature)
	{
		try
		{
			return ((NCombatRoom.Instance?.GetCreatureNode(creature))?.Visuals?.SpineBody)?.HasAnimation("ExpungeSlash") ?? false;
		}
		catch
		{
			return false;
		}
	}

	internal static void TriggerExpungeSlash(Creature creature)
	{
		try
		{
			NCombatRoom.Instance?.GetCreatureNode(creature)?.SetAnimationTrigger("ExpungeSlash");
		}
		catch
		{
		}
	}

	internal static Vector2 StaffAxis(Creature creature)
	{
		try
		{
			Node2D node2D = (NCombatRoom.Instance?.GetCreatureNode(creature))?.Visuals;
			if (node2D != null)
			{
				Node2D nodeOrNull = node2D.GetNodeOrNull<Node2D>("Visuals/WristAnchor");
				if (nodeOrNull != null && GodotObject.IsInstanceValid(nodeOrNull) && nodeOrNull.Position.LengthSquared() > 1f)
				{
					Vector2 vector = StaffEyePosition(creature) - nodeOrNull.GlobalPosition;
					if (vector.LengthSquared() > 1f)
					{
						return vector.Normalized();
					}
				}
			}
		}
		catch
		{
		}
		return Vector2.Right;
	}

	internal static Vector2 StaffGripPosition(Creature creature)
	{
		try
		{
			Node2D node2D = (NCombatRoom.Instance?.GetCreatureNode(creature))?.Visuals;
			if (node2D != null)
			{
				Node2D nodeOrNull = node2D.GetNodeOrNull<Node2D>("Visuals/WristAnchor");
				if (nodeOrNull != null && GodotObject.IsInstanceValid(nodeOrNull) && nodeOrNull.Position.LengthSquared() > 1f)
				{
					return nodeOrNull.GlobalPosition;
				}
			}
		}
		catch
		{
		}
		return StaffEyePosition(creature);
	}

	internal static void PlayEnergyBlade(Creature caster, double realSeconds)
	{
		Emit(new StsEnergyBladeEffect(caster, realSeconds));
		WatcherAudioHelper.PlayOneShot("res://audio/combat/expunge_beam.ogg", 0.55f);
	}

	internal static void PlayBladeImpact(Creature caster, Creature target)
	{
		Vector2 vector = CreatureCenter(target);
		Vector2 bladeAxis = StaffAxis(caster);
		float rotation = Mathf.RadToDeg(bladeAxis.Rotated(Mathf.DegToRad(90f)).Angle());
		Emit(new StsAnimatedSlashEffect(vector, Vector2.Zero, rotation, 4.2f, BladeGlow, BladeCore));
		Emit(new StsBladeCutEffect(vector, bladeAxis));
		for (int i = 0; i < 12; i++)
		{
			Emit(new StsSimpleSparkleEffect(vector + new Vector2(R(-55f, 55f), R(-55f, 55f)), (i % 2 == 0) ? BladeCore : BladeGlow));
		}
		WatcherAudioHelper.PlayOneShot("res://audio/combat/expunge_iron.ogg", 0.35f);
	}

	internal static void PlayJudgment(Creature caster, Creature target, string title)
	{
		if (!NonInteractiveMode.IsActive && NCombatRoom.Instance != null)
		{
			Rect2 rect = CreatureBounds(target);
			float x = rect.Position.X + rect.Size.X / 2f;
			Vector2 hit = new Vector2(x, rect.Position.Y + rect.Size.Y * 0.62f);
			Vector2 textAt = new Vector2(x, Math.Max(130f, rect.Position.Y - 30f));
			float scale = Math.Clamp(rect.Size.X / 230f, 0.9f, 1.6f);
			JudgmentImpactContext impact = new JudgmentImpactContext(caster, hit, textAt, scale, title);
			FireAndForget(JudgmentDrop(hit, scale, impact));
		}
	}

	private static async Task JudgmentDrop(Vector2 hit, float scale, JudgmentImpactContext impact)
	{
		await WaitRealSeconds(0.05999999865889549);
		if (NCombatRoom.Instance != null)
		{
			Emit(new StsJudgmentHammerEffect(hit, scale, impact));
			WatcherAudioHelper.PlayOneShot("res://audio/combat/judgment_fall.ogg", 0.8f);
		}
	}

	private static void JudgmentImpact(in JudgmentImpactContext ctx)
	{
		if (NCombatRoom.Instance != null)
		{
			Vector2 hit = ctx.Hit;
			float scale = ctx.Scale;
			WatcherCinematicHelper.PlayImpactStop(ctx.Caster, 0.08, new Color(1f, 0.93f, 0.75f, 0.16f), 0.14);
			try
			{
				NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Short);
			}
			catch
			{
			}
			Emit(new StsShockRingEffect(hit, Vector2.Down));
			Emit(new StsJudgmentTextEffect(ctx.TextAt, ctx.Title));
			for (int i = 0; i < 18; i++)
			{
				Emit(new StsSimpleSparkleEffect(hit + new Vector2(R(-150f, 150f) * scale, R(-70f, 50f)), (i % 3 == 0) ? Colors.White : JudgeGold));
			}
			for (int j = 0; j < 14; j++)
			{
				Emit(new StsJudgmentMoteEffect(hit + new Vector2(R(-170f, 170f) * scale, R(-30f, 40f)), R(180f, 420f), (j % 4 == 0) ? Colors.White : JudgeGold));
			}
			WatcherAudioHelper.PlayOneShot("res://audio/combat/judgment_impact.ogg");
			WatcherAudioHelper.PlayOneShot("res://audio/combat/expunge_iron.ogg", 0.6f);
		}
	}

	internal static Vector2 StaffEyePosition(Creature creature)
	{
		Vector2 result = CreatureCenter(creature) + new Vector2(-120f, -160f);
		Node2D node2D = (NCombatRoom.Instance?.GetCreatureNode(creature))?.Visuals;
		if (node2D == null)
		{
			return result;
		}
		Node2D node2D2 = node2D.GetNodeOrNull<Node2D>("Visuals/EyeAnchor") ?? FindDescendant(node2D, "EyeAnchor");
		if (node2D2 == null || !GodotObject.IsInstanceValid(node2D2))
		{
			return result;
		}
		if (node2D2.Position.LengthSquared() < 1f)
		{
			return result;
		}
		return node2D2.GlobalPosition;
	}

	private static Node2D? FindDescendant(Node root, string name)
	{
		foreach (Node child in root.GetChildren())
		{
			if (child.Name == (StringName)name && child is Node2D result)
			{
				return result;
			}
			Node2D node2D = FindDescendant(child, name);
			if (node2D != null)
			{
				return node2D;
			}
		}
		return null;
	}

	internal static bool HasSignatureSwing(Creature creature)
	{
		try
		{
			return ((NCombatRoom.Instance?.GetCreatureNode(creature))?.Visuals?.SpineBody)?.HasAnimation("SignatureMove") ?? false;
		}
		catch
		{
			return false;
		}
	}

	internal static void TriggerSignatureSwing(Creature creature)
	{
		try
		{
			NCombatRoom.Instance?.GetCreatureNode(creature)?.SetAnimationTrigger("SignatureMove");
		}
		catch
		{
		}
	}

	internal static void SetStaffEyeAnimation(Creature creature, string animationName)
	{
		try
		{
			Node2D node2D = (NCombatRoom.Instance?.GetCreatureNode(creature))?.Visuals;
			if (node2D != null)
			{
				Node2D nodeOrNull = node2D.GetNodeOrNull<Node2D>("Visuals");
				if (nodeOrNull != null)
				{
					WatcherSkeletonHelper.UpdateEyeTop(nodeOrNull, animationName);
				}
			}
		}
		catch
		{
		}
	}

	internal static void PlayEyeRadiance(Creature caster, double realSeconds)
	{
		Emit(new StsEyeRadianceEffect(caster, realSeconds));
	}

	internal static void PlaySignatureTrail(Creature caster, double duration)
	{
		Emit(new StsStaffTrailEffect(caster, duration));
	}

	internal static void PlaySignatureDischarge(Creature caster, Creature target)
	{
		Vector2 vector = StaffEyePosition(caster);
		Vector2 vector2 = CreatureCenter(target);
		Vector2 dir = (vector2 - vector).Normalized();
		if (dir.LengthSquared() < 0.01f)
		{
			dir = Vector2.Right;
		}
		Emit(new StsStaffFlashEffect(caster));
		Emit(new StsShockRingEffect(vector, dir));
		Emit(new StsLightningBolt(caster, vector2, 0.42f, 1f, 3));
		for (int i = 0; i < 4; i++)
		{
			float angle = Mathf.DegToRad(R(-78f, 78f));
			Vector2 vector3 = dir.Rotated(angle);
			Vector2 end = vector + vector3 * R(360f, 760f);
			Emit(new StsLightningBolt(caster, end, R(0.2f, 0.32f), R(0.4f, 0.62f), 1));
		}
		for (int j = 0; j < 14; j++)
		{
			Emit(new StsSimpleSparkleEffect(vector + new Vector2(R(-45f, 45f), R(-45f, 45f)), (j % 3 == 0) ? BurstGold : BoltMid));
		}
		WatcherAudioHelper.PlayOneShot("res://audio/combat/lightning_evoke.ogg", 0.9f);
		WatcherAudioHelper.PlayOneShot("res://audio/combat/expunge_iron.ogg", 0.35f);
		try
		{
			NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Short);
		}
		catch
		{
		}
	}

	internal static void PlaySignatureImpact(Creature target)
	{
		Vector2 vector = CreatureCenter(target);
		Emit(new StsStarBurstEffect(vector));
		for (int i = 0; i < 18; i++)
		{
			Emit(new StsSimpleSparkleEffect(vector + new Vector2(R(-70f, 70f), R(-70f, 70f)), (i % 2 == 0) ? BurstGold : BoltOuter));
		}
	}

	internal static void PlaySignatureWave(Creature caster, Creature target)
	{
		Vector2 vector = StaffEyePosition(caster);
		Vector2 vector2 = CreatureCenter(target);
		Vector2 vector3 = vector2 - vector;
		if (vector3.LengthSquared() < 1f)
		{
			vector3 = Vector2.Right;
		}
		vector3 = vector3.Normalized();
		Emit(new StsStaffFlashEffect(caster));
		Emit(new StsShockRingEffect(vector, vector3));
		Emit(new StsWaveProjectileEffect(vector, vector2, 0.26f));
		Vector2 vector4 = new Vector2(0f - vector3.Y, vector3.X);
		for (int i = 0; i < 14; i++)
		{
			Vector2 velocity = (vector3 * R(0.6f, 1.2f) + vector4 * R(-0.7f, 0.7f)).Normalized() * R(600f, 1300f);
			Color color = ((i % 3 == 0) ? BurstGold : ((i % 2 == 0) ? BoltOuter : BoltMid));
			Emit(new StsBeamShardEffect(vector, velocity, color, R(0.2f, 0.34f), R(0.7f, 1.5f)));
		}
		WatcherAudioHelper.PlayOneShot("res://audio/combat/expunge_beam.ogg", 0.7f);
		FireAndForget(WaveLand(vector2, vector3));
	}

	private static async Task WaveLand(Vector2 hit, Vector2 dir)
	{
		await WaitRealSeconds(0.25999999046325684);
		if (NCombatRoom.Instance == null)
		{
			return;
		}
		Emit(new StsShockRingEffect(hit, dir));
		Emit(new StsShockRingEffect(hit - dir * 60f, dir));
		Vector2 vector = new Vector2(0f - dir.Y, dir.X);
		for (int i = 0; i < 26; i++)
		{
			Vector2 velocity = (dir * R(-0.2f, 1f) + vector * R(-1f, 1f)).Normalized() * R(450f, 1300f);
			Color color = ((i % 4 == 0) ? BurstGold : ((i % 2 == 0) ? BoltOuter : BoltMid));
			Emit(new StsBeamShardEffect(hit + vector * R(-90f, 90f), velocity, color, R(0.24f, 0.46f), R(0.8f, 1.8f)));
		}
		for (int j = 0; j < 16; j++)
		{
			Emit(new StsSimpleSparkleEffect(hit + new Vector2(R(-110f, 110f), R(-110f, 110f)), (j % 3 == 0) ? BurstGold : BoltMid));
		}
		WatcherAudioHelper.PlayOneShot("res://audio/combat/expunge_iron.ogg", 0.8f);
		WatcherAudioHelper.PlayOneShot("res://audio/combat/lightning_evoke.ogg", 0.7f);
		try
		{
			NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Short);
		}
		catch
		{
		}
	}

	internal static void SetSpineTimeScale(Creature creature, float scale)
	{
		try
		{
			SpineAnimationAccess valueOrDefault = ((NCombatRoom.Instance?.GetCreatureNode(creature))?.Visuals?.SpineAnimation).GetValueOrDefault();
			if (valueOrDefault.IsValid)
			{
				valueOrDefault.SetTimeScale(scale);
			}
		}
		catch
		{
		}
	}

	internal static void PlayStaffAfterimage(Creature caster, Creature target)
	{
		Vector2 vector = CreatureCenter(caster) + new Vector2(0f, -20f);
		Vector2 vector2 = CreatureCenter(target);
		Vector2 aim = (vector2 - vector).Normalized();
		if (aim.LengthSquared() < 0.01f)
		{
			aim = Vector2.Right;
		}
		float num = Math.Max(220f, vector.DistanceTo(vector2));
		float ahead = num + 1700f;
		float num2 = Mathf.Wrap(aim.Angle() - (float)Math.PI / 2f, -(float)Math.PI, (float)Math.PI);
		float num3 = (float)Math.PI / 2f;
		float num4 = (float)Math.PI / 2f + num2 + (float)Mathf.Sign(num2) * Mathf.DegToRad(9f);
		Emit(new StsSwingArcEffect(vector, num3, num4, num * 0.95f, 0.14f));
		for (int i = 0; i < 11; i++)
		{
			float num5 = Lerp(0.1f, 0.94f, (float)i / 10f);
			float num6 = Mathf.Lerp(num3, num4, num5);
			float num7 = Lerp(0.5f, 1f, num5);
			float delay = 0.14f * (1f - MathF.Sqrt(1f - num5));
			Emit(new StsStaffAfterimageEffect(vector, num6, num6, 0f, ahead, 364f, -30f, num7, num7, Lerp(0.3f, 0.62f, num5), Lerp(0.13f, 0.34f, num5), 0.15f, delay));
		}
		Emit(new StsStaffAfterimageEffect(vector, num3, num4, 0.14f, ahead, 520f, -34f, 0.5f, 1f, 1f, 1f, 0.46f, 0f));
		WatcherAudioHelper.PlayOneShot("res://audio/combat/expunge_beam.ogg", 0.55f);
		FireAndForget(BurstOnContact(caster, vector, aim, num, ahead));
	}

	private static async Task BurstOnContact(Creature caster, Vector2 pivot, Vector2 aim, float dist, float ahead)
	{
		await WaitRealSeconds(0.14000000059604645);
		if (NCombatRoom.Instance == null)
		{
			return;
		}
		Emit(new StsStaffFlashEffect(caster));
		Emit(new StsShockRingEffect(pivot + aim * (dist * 0.5f), aim));
		Vector2 vector = StaffEyePosition(caster);
		for (int i = 0; i < 3; i++)
		{
			Vector2 vector2 = aim.Rotated(Mathf.DegToRad(R(-17f, 17f)));
			Emit(new StsLightningBolt(caster, vector + vector2 * R(dist * 0.6f, dist * 1.3f), R(0.15f, 0.24f), R(0.45f, 0.75f), 1));
		}
		Vector2 vector3 = new Vector2(0f - aim.Y, aim.X);
		for (int j = 0; j < 18; j++)
		{
			Vector2 p = pivot + aim * (R(0.1f, 0.75f) * ahead) + vector3 * R(-70f, 70f);
			Vector2 velocity = (vector3 * R(-1f, 1f) + aim * R(0.15f, 0.9f)).Normalized() * R(420f, 1150f);
			Color color = ((j % 4 == 0) ? BurstGold : ((j % 2 == 0) ? BoltOuter : BoltMid));
			Emit(new StsBeamShardEffect(p, velocity, color, R(0.22f, 0.42f), R(0.6f, 1.5f)));
		}
		for (int k = 0; k < 10; k++)
		{
			Emit(new StsSimpleSparkleEffect(vector + new Vector2(R(-60f, 60f), R(-60f, 60f)), (k % 3 == 0) ? BurstGold : BoltMid));
		}
		WatcherAudioHelper.PlayOneShot("res://audio/combat/expunge_iron.ogg", 0.75f);
		WatcherAudioHelper.PlayOneShot("res://audio/combat/lightning_evoke.ogg", 0.7f);
		try
		{
			NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Short);
		}
		catch
		{
		}
	}
}
internal static class WatcherAudioHelper
{
	private struct ActiveSound
	{
		public AudioStreamPlayer Player;

		public Callable FinishedCb;
	}

	private static readonly System.Collections.Generic.Dictionary<string, AudioStreamOggVorbis?> StreamCache = new System.Collections.Generic.Dictionary<string, AudioStreamOggVorbis>();

	private static readonly HashSet<string> NegativeCache = new HashSet<string>();

	private static readonly StringName SfxBus = new StringName("SFX");

	private static readonly List<AudioStreamPlayer> FreePool = new List<AudioStreamPlayer>();

	private static readonly List<ActiveSound> Playing = new List<ActiveSound>();

	public static void PlayOneShot(string resPath, float volumeLinear = 1f)
	{
		if (NonInteractiveMode.IsActive)
		{
			return;
		}
		AudioStreamOggVorbis audioStreamOggVorbis = LoadStream(resPath);
		if (audioStreamOggVorbis != null)
		{
			AudioStreamPlayer player;
			if (FreePool.Count > 0)
			{
				player = FreePool[FreePool.Count - 1];
				FreePool.RemoveAt(FreePool.Count - 1);
			}
			else
			{
				player = new AudioStreamPlayer();
				player.Bus = SfxBus;
				((SceneTree)Engine.GetMainLoop()).Root.AddChild(player, forceReadableName: false, Node.InternalMode.Disabled);
			}
			player.Stream = audioStreamOggVorbis;
			player.VolumeDb = Mathf.LinearToDb(volumeLinear);
			Callable callable = Callable.From(delegate
			{
				OnFinished(player);
			});
			player.Connect(AudioStreamPlayer.SignalName.Finished, callable);
			Playing.Add(new ActiveSound
			{
				Player = player,
				FinishedCb = callable
			});
			player.Play();
		}
	}

	private static void OnFinished(AudioStreamPlayer player)
	{
		for (int i = 0; i < Playing.Count; i++)
		{
			if (Playing[i].Player == player)
			{
				player.Disconnect(AudioStreamPlayer.SignalName.Finished, Playing[i].FinishedCb);
				Playing.RemoveAt(i);
				player.Stop();
				FreePool.Add(player);
				break;
			}
		}
	}

	private static AudioStreamOggVorbis? LoadStream(string resPath)
	{
		if (NegativeCache.Contains(resPath))
		{
			return null;
		}
		if (StreamCache.TryGetValue(resPath, out AudioStreamOggVorbis value))
		{
			return value;
		}
		try
		{
			using Godot.FileAccess fileAccess = Godot.FileAccess.Open(resPath, Godot.FileAccess.ModeFlags.Read);
			if (fileAccess == null)
			{
				NegativeCache.Add(resPath);
				return null;
			}
			AudioStreamOggVorbis audioStreamOggVorbis = AudioStreamOggVorbis.LoadFromBuffer(fileAccess.GetBuffer((long)fileAccess.GetLength()));
			StreamCache[resPath] = audioStreamOggVorbis;
			return audioStreamOggVorbis;
		}
		catch
		{
			NegativeCache.Add(resPath);
			return null;
		}
	}
}
internal static class WatcherCardArtSettings
{
	private class SettingsData
	{
		public bool GlobalHandDrawn { get; set; }

		public System.Collections.Generic.Dictionary<string, bool> PerCard { get; set; } = new System.Collections.Generic.Dictionary<string, bool>();
	}

	private static readonly string SettingsPath = Path.Combine(OS.GetUserDataDir(), "watcher_card_art.json");

	private static System.Collections.Generic.Dictionary<string, bool>? _perCardSettings;

	private static bool _globalHandDrawn;

	private static bool _loaded;

	public const string PlaceholderPortraitPath = "res://images/packed/card_portraits/watcher/_placeholder.png";

	public static bool GlobalHandDrawn
	{
		get
		{
			EnsureLoaded();
			return _globalHandDrawn;
		}
		set
		{
			EnsureLoaded();
			_globalHandDrawn = value;
			Save();
		}
	}

	private static void EnsureLoaded()
	{
		if (_loaded)
		{
			return;
		}
		_loaded = true;
		_perCardSettings = new System.Collections.Generic.Dictionary<string, bool>();
		try
		{
			if (!File.Exists(SettingsPath))
			{
				return;
			}
			SettingsData settingsData = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(SettingsPath));
			if (settingsData != null)
			{
				_globalHandDrawn = settingsData.GlobalHandDrawn;
				if (settingsData.PerCard != null)
				{
					_perCardSettings = new System.Collections.Generic.Dictionary<string, bool>(settingsData.PerCard);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] Failed to load card art settings: " + ex.Message);
		}
	}

	private static void Save()
	{
		try
		{
			string contents = JsonSerializer.Serialize(new SettingsData
			{
				GlobalHandDrawn = _globalHandDrawn,
				PerCard = (_perCardSettings ?? new System.Collections.Generic.Dictionary<string, bool>())
			}, new JsonSerializerOptions
			{
				WriteIndented = true
			});
			string directoryName = Path.GetDirectoryName(SettingsPath);
			if (directoryName != null)
			{
				Directory.CreateDirectory(directoryName);
			}
			File.WriteAllText(SettingsPath, contents);
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] Failed to save card art settings: " + ex.Message);
		}
	}

	public static bool IsHandDrawn(CardModel card)
	{
		EnsureLoaded();
		string key = card.Id.Entry.ToLower();
		if (_perCardSettings != null && _perCardSettings.TryGetValue(key, out var value))
		{
			return value;
		}
		return _globalHandDrawn;
	}

	public static bool HasPerCardOverride(CardModel card)
	{
		EnsureLoaded();
		string key = card.Id.Entry.ToLower();
		return _perCardSettings?.ContainsKey(key) ?? false;
	}

	public static bool ToggleCard(CardModel card)
	{
		EnsureLoaded();
		string key = card.Id.Entry.ToLower();
		bool flag = !IsHandDrawn(card);
		if (_perCardSettings == null)
		{
			_perCardSettings = new System.Collections.Generic.Dictionary<string, bool>();
		}
		if (flag == _globalHandDrawn)
		{
			_perCardSettings.Remove(key);
		}
		else
		{
			_perCardSettings[key] = flag;
		}
		Save();
		return flag;
	}

	public static string GetEffectivePortraitPath(CardModel card)
	{
		string portraitPath = card.PortraitPath;
		if (IsHandDrawn(card))
		{
			string betaPortraitPath = GetBetaPortraitPath(card);
			if (WatcherTextureHelper.LoadTexture(betaPortraitPath) != null)
			{
				return betaPortraitPath;
			}
		}
		if (WatcherTextureHelper.LoadTexture(portraitPath) != null)
		{
			return portraitPath;
		}
		return "res://images/packed/card_portraits/watcher/_placeholder.png";
	}

	public static string GetBetaPortraitPath(CardModel card)
	{
		return card.BetaPortraitPath;
	}

	public static bool HasBetaArt(CardModel card)
	{
		return WatcherTextureHelper.LoadTexture(GetBetaPortraitPath(card)) != null;
	}
}
internal static class WatcherCardCompat
{
	private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

	private static readonly PropertyInfo? CardCombatStateProperty = typeof(CardModel).GetProperty("CombatState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

	private static readonly FieldInfo? CardCombatStateBackingField = typeof(CardModel).GetField("<CombatState>k__BackingField", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? typeof(CardModel).GetField("_combatState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? typeof(CardModel).GetField("CombatState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

	public static CombatState? GetCombatState(CardModel? card)
	{
		if (card == null)
		{
			return null;
		}
		if (TryReadCombatState(() => CardCombatStateProperty?.GetValue(card), out CombatState combat))
		{
			return combat;
		}
		if (TryReadCombatState(() => CardCombatStateBackingField?.GetValue(card), out combat))
		{
			return combat;
		}
		return WatcherCreatureCompat.GetCombatState(card.Owner?.Creature);
	}

	public static CombatState RequireCombatState(CardModel? card, string message)
	{
		return GetCombatState(card) ?? throw new InvalidOperationException(message);
	}

	private static bool TryReadCombatState(Func<object?> read, out CombatState? combat)
	{
		combat = null;
		try
		{
			combat = read() as CombatState;
			return combat != null;
		}
		catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is MissingMethodException || ex is TargetInvocationException)
		{
			return false;
		}
	}
}
internal static class WatcherCardPileCompat
{
	[CompilerGenerated]

	private static readonly MethodInfo? AddGeneratedCardWithCreator = FindAddGeneratedCardToCombat(typeof(Player));

	private static readonly MethodInfo? AddGeneratedCardWithAddedByPlayer = FindAddGeneratedCardToCombat(typeof(bool));

	internal static IEnumerable<MethodBase> AddGeneratedCardToCombatTargets()
	{
		if (AddGeneratedCardWithCreator != null)
		{
			yield return AddGeneratedCardWithCreator;
		}
		if (AddGeneratedCardWithAddedByPlayer != null)
		{
			yield return AddGeneratedCardWithAddedByPlayer;
		}
	}

	public static Task<CardPileAddResult> AddGeneratedCardToCombat(CardModel card, PileType newPileType, bool addedByPlayer, CardPilePosition position = CardPilePosition.Bottom)
	{
		Player creator = (addedByPlayer ? card.Owner : null);
		return AddGeneratedCardToCombat(card, newPileType, creator, addedByPlayer, position);
	}

	public static Task<CardPileAddResult> AddGeneratedCardToCombat(CardModel card, PileType newPileType, Player? creator, CardPilePosition position = CardPilePosition.Bottom)
	{
		return AddGeneratedCardToCombat(card, newPileType, creator, creator != null, position);
	}

	private static async Task<CardPileAddResult> AddGeneratedCardToCombat(CardModel card, PileType newPileType, Player? creator, bool addedByPlayer, CardPilePosition position)
	{
		CardPileAddResult result;
		if (AddGeneratedCardWithCreator != null)
		{
			result = await InvokeAddGeneratedCard(AddGeneratedCardWithCreator, card, newPileType, creator, position);
		}
		else
		{
			if (!(AddGeneratedCardWithAddedByPlayer != null))
			{
				throw new MissingMethodException(typeof(CardPileCmd).FullName, "AddGeneratedCardToCombat(CardModel, PileType, Player|bool, CardPilePosition)");
			}
			result = await InvokeAddGeneratedCard(AddGeneratedCardWithAddedByPlayer, card, newPileType, addedByPlayer, position);
		}
		if (newPileType == PileType.Draw)
		{
			CardCmd.PreviewCardPileAdd(result);
		}
		return result;
	}

	private static Task<CardPileAddResult> InvokeAddGeneratedCard(MethodInfo method, CardModel card, PileType newPileType, object? creatorOrAddedByPlayer, CardPilePosition position)
	{
		object obj = method.Invoke(null, new object[4] { card, newPileType, creatorOrAddedByPlayer, position });
		if (obj is Task<CardPileAddResult> result)
		{
			return result;
		}
		throw new InvalidOperationException("AddGeneratedCardToCombat returned unexpected type " + (obj?.GetType().FullName ?? "<null>") + ".");
	}

	private static MethodInfo? FindAddGeneratedCardToCombat(Type thirdParameterType)
	{
		return typeof(CardPileCmd).GetMethod("AddGeneratedCardToCombat", BindingFlags.Static | BindingFlags.Public, null, new Type[4]
		{
			typeof(CardModel),
			typeof(PileType),
			thirdParameterType,
			typeof(CardPilePosition)
		}, null);
	}
}
internal sealed class WatcherCharSelectSpineFitter
{
	private const float DesignW = 1920f;

	private const float DesignH = 1080f;

	private static readonly Vector2 DesignCenter = new Vector2(960f, 540f);

	private readonly Node2D _spine;

	private readonly Vector2 _designPos;

	private readonly Vector2 _designScale;

	private bool _disposed;

	public Node2D Spine => _spine;

	public bool IsAlive
	{
		get
		{
			if (!_disposed && GodotObject.IsInstanceValid(_spine))
			{
				return _spine.IsInsideTree();
			}
			return false;
		}
	}

	private WatcherCharSelectSpineFitter(Node2D spine)
	{
		_spine = spine;
		_designPos = spine.Position;
		_designScale = spine.Scale;
	}

	public static void Attach(Node2D? spine)
	{
		if (spine != null && GodotObject.IsInstanceValid(spine))
		{
			WatcherCharSelectSpineFitter watcherCharSelectSpineFitter = new WatcherCharSelectSpineFitter(spine);
			if (WatcherCharSelectSpineFitManager.Register(watcherCharSelectSpineFitter))
			{
				watcherCharSelectSpineFitter.Apply();
				spine.TreeExiting += watcherCharSelectSpineFitter.Dispose;
			}
		}
	}

	public void Tick()
	{
		Apply();
	}

	private void Apply()
	{
		if (!IsAlive)
		{
			Dispose();
			return;
		}
		Vector2 size = _spine.GetViewportRect().Size;
		if (!(size.X <= 0f) && !(size.Y <= 0f))
		{
			float num = Mathf.Max(size.X / 1920f, size.Y / 1080f);
			_spine.Scale = _designScale * num;
			_spine.Position = (_designPos - DesignCenter) * num + size * 0.5f;
		}
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			WatcherCharSelectSpineFitManager.Unregister(this);
		}
	}
}
internal static class WatcherCharSelectSpineFitManager
{
	private static readonly List<WatcherCharSelectSpineFitter> _fitters = new List<WatcherCharSelectSpineFitter>();

	private static bool _hooked;

	public static bool Register(WatcherCharSelectSpineFitter fitter)
	{
		for (int num = _fitters.Count - 1; num >= 0; num--)
		{
			if (!_fitters[num].IsAlive)
			{
				_fitters[num].Dispose();
				_fitters.RemoveAt(num);
			}
			else if (_fitters[num].Spine == fitter.Spine)
			{
				return false;
			}
		}
		EnsureHooked();
		_fitters.Add(fitter);
		return true;
	}

	public static void Unregister(WatcherCharSelectSpineFitter fitter)
	{
		_fitters.Remove(fitter);
	}

	private static void EnsureHooked()
	{
		if (!_hooked && Engine.GetMainLoop() is SceneTree sceneTree)
		{
			sceneTree.ProcessFrame += Tick;
			_hooked = true;
		}
	}

	private static void Tick()
	{
		for (int num = _fitters.Count - 1; num >= 0; num--)
		{
			WatcherCharSelectSpineFitter watcherCharSelectSpineFitter = _fitters[num];
			if (!watcherCharSelectSpineFitter.IsAlive)
			{
				watcherCharSelectSpineFitter.Dispose();
				_fitters.RemoveAt(num);
			}
			else
			{
				try
				{
					watcherCharSelectSpineFitter.Tick();
				}
				catch (Exception value)
				{
					Log.Error($"[Watcher] char-select spine fit failed: {value}");
				}
			}
		}
	}
}
internal static class WatcherCinematicHelper
{
	private readonly record struct Key(double Time, double Value);

	private readonly record struct Flash(double Time, double Duration, Color Color);

	internal const double FreezeAt = 0.32;

	internal const double BuildAt = 0.48;

	internal const double DrainAt = 2.14;

	internal const double ReleaseAt = 2.31;

	internal const double DischargeAt = 2.51;

	internal const double RecoverAt = 2.78;

	private const double MaxCutSeconds = 5.0;

	private const string GradeShader = "\r\nshader_type canvas_item;\r\nuniform sampler2D screen_tex : hint_screen_texture, filter_linear;\r\nuniform float blue : hint_range(0.0, 1.0) = 0.0;\r\nuniform float mono : hint_range(0.0, 1.0) = 0.0;\r\nuniform float contrast : hint_range(0.5, 4.0) = 1.5;\r\nvoid fragment() {\r\n    vec4 src = texture(screen_tex, SCREEN_UV);\r\n    float g = dot(src.rgb, vec3(0.299, 0.587, 0.114));\r\n    // Cold blue grade: desaturate, then push the result toward moonlight.\r\n    vec3 chilled = vec3(g * 0.38, g * 0.66, min(1.0, g * 1.28 + 0.06));\r\n    vec3 outc = mix(src.rgb, chilled, blue);\r\n    // Stark monochrome for the impact frames, with the contrast cranked.\r\n    float m = clamp((g - 0.5) * contrast + 0.5, 0.0, 1.0);\r\n    outc = mix(outc, vec3(m), mono);\r\n    COLOR = vec4(outc, 1.0);\r\n}";

	private static CanvasLayer? _layer;

	private static ColorRect? _grade;

	private static ColorRect? _flash;

	private static ShaderMaterial? _gradeMaterial;

	private static bool _hooked;

	private static bool _running;

	private static bool _swingFrozen;

	private static Creature? _caster;

	private static double _startTime;

	private static double _restoreTo = 1.0;

	private static Key[] _timeScaleKeys = System.Array.Empty<Key>();

	private static Key[] _blueKeys = System.Array.Empty<Key>();

	private static Key[] _monoKeys = System.Array.Empty<Key>();

	private static Flash[] _flashKeys = System.Array.Empty<Flash>();

	private const double MaxStopSeconds = 0.6;

	private static bool _stopRunning;

	private static double _stopStart;

	private static double _stopHold;

	private static double _stopFlashSeconds;

	private static Color _stopFlash;

	private static double _stopRestoreTo = 1.0;

	internal static bool ImpactCutsEnabled
	{
		get
		{
			if (NonInteractiveMode.IsActive)
			{
				return false;
			}
			try
			{
				PrefsSave prefsSave = SaveManager.Instance.PrefsSave;
				return prefsSave.ScreenShakeOptionIndex != 0 && prefsSave.FastMode != FastModeType.Instant;
			}
			catch
			{
				return false;
			}
		}
	}

	internal static bool ShouldPlayCutFor(Creature caster)
	{
		if (!ImpactCutsEnabled)
		{
			return false;
		}
		try
		{
			return !LocalContext.NetId.HasValue || LocalContext.IsMe(caster);
		}
		catch
		{
			return false;
		}
	}

	internal static void PlaySignatureCut(Creature caster)
	{
		if (ImpactCutsEnabled && Engine.GetMainLoop() is SceneTree tree && EnsureOverlay(tree))
		{
			if (_stopRunning)
			{
				RestoreStop();
			}
			if (!_running)
			{
				_restoreTo = Engine.TimeScale;
			}
			_caster = caster;
			_timeScaleKeys = new Key[6]
			{
				new Key(0.0, 1.0),
				new Key(0.31, 1.0),
				new Key(0.32, 0.03),
				new Key(2.3000000000000003, 0.03),
				new Key(2.31, 1.0),
				new Key(2.78, 1.0)
			};
			_blueKeys = new Key[6]
			{
				new Key(0.0, 0.0),
				new Key(0.32, 0.35),
				new Key(0.44, 0.9),
				new Key(2.14, 0.75),
				new Key(2.25, 0.0),
				new Key(2.78, 0.0)
			};
			_monoKeys = new Key[5]
			{
				new Key(0.0, 0.0),
				new Key(2.14, 0.2),
				new Key(2.25, 1.0),
				new Key(2.61, 1.0),
				new Key(2.78, 0.0)
			};
			_flashKeys = new Flash[2]
			{
				new Flash(2.25, 0.06, new Color(0.01f, 0.01f, 0.04f, 0.55f)),
				new Flash(2.31, 0.09, new Color(0.92f, 0.94f, 1f, 0.3f))
			};
			_startTime = Now();
			_running = true;
			_swingFrozen = false;
			EnsureHooked(tree);
		}
	}

	private static void SetSwingFrozen(bool frozen)
	{
		if (_swingFrozen == frozen)
		{
			return;
		}
		_swingFrozen = frozen;
		if (_caster == null)
		{
			return;
		}
		try
		{
			SpineAnimationAccess valueOrDefault = ((NCombatRoom.Instance?.GetCreatureNode(_caster))?.Visuals?.SpineAnimation).GetValueOrDefault();
			if (valueOrDefault.IsValid)
			{
				valueOrDefault.SetTimeScale(frozen ? 0f : 1f);
			}
		}
		catch
		{
			_swingFrozen = false;
		}
	}

	internal static void PlayImpactStop(Creature caster, double holdSeconds, Color flash, double flashSeconds)
	{
		if (ShouldPlayCutFor(caster) && !_running && Engine.GetMainLoop() is SceneTree tree && EnsureOverlay(tree))
		{
			if (!_stopRunning)
			{
				_stopRestoreTo = Engine.TimeScale;
			}
			_stopStart = Now();
			_stopHold = Math.Clamp(holdSeconds, 0.0, 0.6);
			_stopFlash = flash;
			_stopFlashSeconds = Math.Clamp(flashSeconds, 0.0, 0.6);
			_stopRunning = true;
			EnsureHooked(tree);
		}
	}

	private static void RestoreStop()
	{
		_stopRunning = false;
		Engine.TimeScale = ((_stopRestoreTo <= 0.0) ? 1.0 : _stopRestoreTo);
		_stopRestoreTo = 1.0;
		if (_flash != null && GodotObject.IsInstanceValid(_flash))
		{
			_flash.Visible = false;
		}
	}

	private static void TickStop()
	{
		double num = Now() - _stopStart;
		double num2 = Math.Max(_stopHold, _stopFlashSeconds);
		if (num >= num2 || num >= 0.6 || NonInteractiveMode.IsActive || NCombatRoom.Instance == null)
		{
			RestoreStop();
			return;
		}
		double num3 = ((_stopRestoreTo <= 0.0) ? 1.0 : _stopRestoreTo);
		Engine.TimeScale = ((num < _stopHold) ? 0.04 : num3);
		if (_flash != null && GodotObject.IsInstanceValid(_flash))
		{
			float num4 = ((_stopFlashSeconds > 0.0) ? ((float)Math.Clamp(1.0 - num / _stopFlashSeconds, 0.0, 1.0)) : 0f);
			num4 *= num4;
			Color stopFlash = _stopFlash;
			stopFlash.A = _stopFlash.A * num4;
			_flash.Visible = stopFlash.A > 0.002f;
			_flash.Color = stopFlash;
		}
	}

	internal static void Restore()
	{
		_stopRunning = false;
		_stopRestoreTo = 1.0;
		_running = false;
		SetSwingFrozen(frozen: false);
		_caster = null;
		Engine.TimeScale = ((_restoreTo <= 0.0) ? 1.0 : _restoreTo);
		_restoreTo = 1.0;
		if (_gradeMaterial != null)
		{
			_gradeMaterial.SetShaderParameter("blue", 0f);
			_gradeMaterial.SetShaderParameter("mono", 0f);
		}
		if (_grade != null && GodotObject.IsInstanceValid(_grade))
		{
			_grade.Visible = false;
		}
		if (_flash != null && GodotObject.IsInstanceValid(_flash))
		{
			_flash.Visible = false;
		}
	}

	private static double Now()
	{
		return (double)Time.GetTicksMsec() / 1000.0;
	}

	private static bool EnsureOverlay(SceneTree tree)
	{
		if (_layer != null && GodotObject.IsInstanceValid(_layer))
		{
			return true;
		}
		try
		{
			_layer = new CanvasLayer
			{
				Layer = 90,
				Name = "WatcherSignatureCut"
			};
			Shader shader = new Shader
			{
				Code = "\r\nshader_type canvas_item;\r\nuniform sampler2D screen_tex : hint_screen_texture, filter_linear;\r\nuniform float blue : hint_range(0.0, 1.0) = 0.0;\r\nuniform float mono : hint_range(0.0, 1.0) = 0.0;\r\nuniform float contrast : hint_range(0.5, 4.0) = 1.5;\r\nvoid fragment() {\r\n    vec4 src = texture(screen_tex, SCREEN_UV);\r\n    float g = dot(src.rgb, vec3(0.299, 0.587, 0.114));\r\n    // Cold blue grade: desaturate, then push the result toward moonlight.\r\n    vec3 chilled = vec3(g * 0.38, g * 0.66, min(1.0, g * 1.28 + 0.06));\r\n    vec3 outc = mix(src.rgb, chilled, blue);\r\n    // Stark monochrome for the impact frames, with the contrast cranked.\r\n    float m = clamp((g - 0.5) * contrast + 0.5, 0.0, 1.0);\r\n    outc = mix(outc, vec3(m), mono);\r\n    COLOR = vec4(outc, 1.0);\r\n}"
			};
			_gradeMaterial = new ShaderMaterial
			{
				Shader = shader
			};
			_gradeMaterial.SetShaderParameter("blue", 0f);
			_gradeMaterial.SetShaderParameter("mono", 0f);
			_gradeMaterial.SetShaderParameter("contrast", 1.55f);
			_grade = new ColorRect
			{
				Name = "Grade",
				Color = Colors.White,
				Material = _gradeMaterial,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				Visible = false
			};
			_grade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			_flash = new ColorRect
			{
				Name = "Flash",
				Color = new Color(1f, 1f, 1f, 0f),
				MouseFilter = Control.MouseFilterEnum.Ignore,
				Visible = false
			};
			_flash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
			_layer.AddChild(_grade, forceReadableName: false, Node.InternalMode.Disabled);
			_layer.AddChild(_flash, forceReadableName: false, Node.InternalMode.Disabled);
			tree.Root.AddChild(_layer, forceReadableName: false, Node.InternalMode.Disabled);
			return true;
		}
		catch
		{
			_layer = null;
			_grade = null;
			_flash = null;
			_gradeMaterial = null;
			return false;
		}
	}

	private static void EnsureHooked(SceneTree tree)
	{
		if (!_hooked)
		{
			tree.ProcessFrame += Tick;
			_hooked = true;
		}
	}

	private static void Tick()
	{
		if (_stopRunning && !_running)
		{
			try
			{
				TickStop();
			}
			catch
			{
				RestoreStop();
			}
		}
		if (!_running)
		{
			return;
		}
		try
		{
			double num = Now() - _startTime;
			if (num >= 2.78 || num >= 5.0 || NonInteractiveMode.IsActive || NCombatRoom.Instance == null)
			{
				Restore();
				return;
			}
			Engine.TimeScale = Math.Clamp(Sample(_timeScaleKeys, num, 1.0), 0.02, 4.0);
			SetSwingFrozen(num >= 0.32 && num < 2.31);
			float num2 = (float)Math.Clamp(Sample(_blueKeys, num, 0.0), 0.0, 1.0);
			float num3 = (float)Math.Clamp(Sample(_monoKeys, num, 0.0), 0.0, 1.0);
			if (_grade != null && GodotObject.IsInstanceValid(_grade))
			{
				_grade.Visible = num2 > 0.001f || num3 > 0.001f;
				_gradeMaterial?.SetShaderParameter("blue", num2);
				_gradeMaterial?.SetShaderParameter("mono", num3);
			}
			if (_flash != null && GodotObject.IsInstanceValid(_flash))
			{
				Color color = SampleFlash(num);
				_flash.Visible = color.A > 0.002f;
				_flash.Color = color;
			}
		}
		catch
		{
			Restore();
		}
	}

	private static double Sample(Key[] keys, double t, double fallback)
	{
		if (keys.Length == 0)
		{
			return fallback;
		}
		if (t <= keys[0].Time)
		{
			return keys[0].Value;
		}
		for (int i = 0; i < keys.Length - 1; i++)
		{
			Key key = keys[i];
			Key key2 = keys[i + 1];
			if (!(t < key.Time) && !(t > key2.Time))
			{
				double num = key2.Time - key.Time;
				if (num <= 0.0)
				{
					return key2.Value;
				}
				double num2 = (t - key.Time) / num;
				num2 = 1.0 - Math.Pow(1.0 - num2, 2.0);
				return key.Value + (key2.Value - key.Value) * num2;
			}
		}
		return keys[^1].Value;
	}

	private static Color SampleFlash(double t)
	{
		Color result = new Color(1f, 1f, 1f, 0f);
		Flash[] flashKeys = _flashKeys;
		for (int i = 0; i < flashKeys.Length; i++)
		{
			Flash flash = flashKeys[i];
			if (!(t < flash.Time) && !(t > flash.Time + flash.Duration))
			{
				float num = (float)(1.0 - (t - flash.Time) / flash.Duration);
				num *= num;
				if (flash.Color.A * num > result.A)
				{
					result = new Color(flash.Color.R, flash.Color.G, flash.Color.B, flash.Color.A * num);
				}
			}
		}
		return result;
	}
}
public interface IOnScryDiscarded
{
	Task OnScryDiscarded(PlayerChoiceContext choiceContext, Player owner);
}
internal static class WatcherCombatHelper
{
	internal static LocString ScrySelectionPrompt => LocString.GetIfExists("powers", "SCRY.selectionScreenPrompt") ?? new LocString("card_selection", "TO_DISCARD");

	internal static void DeferRetainCard(Player owner, CardModel card)
	{
		if (owner != null)
		{
			(owner.Creature?.GetPower<WatcherStatePower>())?.DeferRetainCard(card);
		}
	}

	public static async Task EnterWrath(Player owner, CardModel? source)
	{
		await ChangeStance<Wrath>(owner, source);
	}

	public static async Task EnterCalm(Player owner, CardModel? source)
	{
		await ChangeStance<Calm>(owner, source);
	}

	public static async Task EnterDivinity(Player owner, CardModel? source)
	{
		await ChangeStance<Divinity>(owner, source);
	}

	public static async Task EnterForeseen(Player owner, CardModel? source)
	{
		await ChangeStance<Foreseen>(owner, source);
	}

	public static async Task ExitStance(Player owner)
	{
		Type currentStance = GetCurrentStance(owner.Creature);
		if (!(currentStance == null))
		{
			await RemoveAllStances(owner.Creature);
			await OnStanceChanged(owner, currentStance, null);
		}
	}

	public static bool IsInStance<T>(Creature creature) where T : PowerModel
	{
		return creature.HasPower<T>();
	}

	public static async Task GainMantra(Player owner, int amount, CardModel? source)
	{
		if (amount > 0)
		{
			WatcherStatePower watcherStatePower = await EnsureState(owner);
			if (watcherStatePower != null)
			{
				watcherStatePower.TotalMantraGainedThisCombat += amount;
				watcherStatePower.MantraGainedThisTurn += amount;
				await WatcherPowerCmdCompat.Apply<Mantra>(owner.Creature, amount, owner.Creature, source);
			}
		}
	}

	public static int GetTotalMantraGained(Player owner)
	{
		return owner.Creature.GetPower<WatcherStatePower>()?.TotalMantraGainedThisCombat ?? 0;
	}

	public static int GetMantraGainedThisTurn(Player owner)
	{
		return owner.Creature.GetPower<WatcherStatePower>()?.MantraGainedThisTurn ?? 0;
	}

	public static async Task<int> ConsumeKnowFate(Player owner, int amount, Creature? applier, CardModel? source, bool silent = false)
	{
		if (amount <= 0)
		{
			return 0;
		}
		WatcherStatePower state = await EnsureState(owner);
		if (state == null)
		{
			return 0;
		}
		state.KnowFateConsumptionAttemptedThisCard = true;
		int powerAmount = owner.Creature.GetPowerAmount<KnowFatePower>();
		if (powerAmount <= 0)
		{
			return 0;
		}
		KnowFatePower power = owner.Creature.GetPower<KnowFatePower>();
		if (power == null)
		{
			return 0;
		}
		int consumed = Math.Min(powerAmount, amount);
		state.KnowFateConsumedThisTurn = true;
		if (consumed < powerAmount)
		{
			await WatcherPowerCmdCompat.ModifyAmount(power, -consumed, applier, source, silent);
		}
		else
		{
			await PowerCmd.Remove(power);
		}
		state.KnowFateLastObserved = owner.Creature.GetPowerAmount<KnowFatePower>();
		return consumed;
	}

	public static int GetAttacksPlayedThisTurn(Player owner)
	{
		return owner.Creature.GetPower<WatcherStatePower>()?.AttacksPlayedThisTurn ?? 0;
	}

	public static int GetProphecyPlaysThisTurn(Player owner)
	{
		return owner.Creature.GetPower<WatcherStatePower>()?.ProphecyPlaysThisTurn ?? 0;
	}

	internal static async Task ResolveImplicitProphecyFinality(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		CardModel card = cardPlay.Card;
		Player owner = card.Owner;
		if (!(card is IProphecyCard) || owner?.Creature == null)
		{
			return;
		}
		WatcherStatePower power = owner.Creature.GetPower<WatcherStatePower>();
		if (power != null && power.KnowFateConsumptionAttemptedThisCard)
		{
			return;
		}
		if (card.GetType().Name == "WatcherOmniscienceV2" && owner.Creature.GetPowerAmount<KnowFatePower>() >= 12)
		{
			if (power != null)
			{
				power.KnowFateConsumptionAttemptedThisCard = true;
			}
			await PlayerCmd.GainEnergy(1m, owner);
			await WatcherPowerCmdCompat.Apply<EnlightenFatePower>(owner.Creature, 2m, owner.Creature, card);
			return;
		}
		int cap = GetImplicitFinalityCap(card);
		if (cap <= 0)
		{
			return;
		}
		int num = await ConsumeKnowFate(owner, cap, owner.Creature, card);
		if (num > 0)
		{
			switch (card.Type)
			{
			case CardType.Attack:
				await ResolveImplicitAttackFinality(choiceContext, cardPlay, num, cap);
				break;
			case CardType.Skill:
				await ResolveImplicitSkillFinality(choiceContext, cardPlay, num, cap);
				break;
			case CardType.Power:
				await WatcherPowerCmdCompat.Apply<EnlightenFatePower>(owner.Creature, Math.Max(1, num), owner.Creature, card);
				break;
			}
		}
	}

	private static int GetImplicitFinalityCap(CardModel card)
	{
		return card.Rarity switch
		{
			CardRarity.Basic => 1, 
			CardRarity.Common => 1, 
			CardRarity.Uncommon => 2, 
			CardRarity.Rare => 3, 
			_ => 1, 
		};
	}

	private static async Task ResolveImplicitAttackFinality(PlayerChoiceContext choiceContext, CardPlay cardPlay, int consumed, int cap)
	{
		CardModel card = cardPlay.Card;
		Player owner = card.Owner;
		CardRarity rarity = card.Rarity;
		decimal damage = (decimal)consumed * rarity switch
		{
			CardRarity.Rare => 4m, 
			CardRarity.Uncommon => 3m, 
			_ => 2m, 
		};
		if (cardPlay.Target?.IsAlive ?? false)
		{
			await DamageCmd.Attack(damage).FromCard(card, cardPlay).Targeting(cardPlay.Target)
				.WithHitFx("vfx/vfx_starry_impact")
				.Execute(choiceContext);
			if ((card.Rarity == CardRarity.Uncommon || card.Rarity == CardRarity.Rare) && cardPlay.Target.IsAlive)
			{
				await WatcherPowerCmdCompat.Apply<VulnerablePower>(cardPlay.Target, 1m, owner.Creature, card);
			}
		}
		else
		{
			CombatState combatState = WatcherCardCompat.GetCombatState(card);
			if (combatState != null)
			{
				await DamageCmd.Attack(damage).FromCard(card, cardPlay).TargetingAllOpponentsCompat(combatState)
					.WithHitFx("vfx/vfx_starry_impact")
					.SpawningHitVfxOnEachCreature()
					.Execute(choiceContext);
			}
		}
		if (card.Rarity == CardRarity.Rare && consumed >= cap)
		{
			await WatcherPowerCmdCompat.Apply<BlessProphecyDamagePower>(owner.Creature, damage, owner.Creature, card);
		}
	}

	private static async Task ResolveImplicitSkillFinality(PlayerChoiceContext choiceContext, CardPlay cardPlay, int consumed, int cap)
	{
		CardModel card = cardPlay.Card;
		Player owner = card.Owner;
		if (!(cardPlay.Target?.IsAlive ?? false) || card.TargetType != TargetType.AnyEnemy)
		{
			await CardPileCmd.Draw(choiceContext, Math.Min(consumed, 2), owner);
		}
		else
		{
			await WatcherPowerCmdCompat.Apply<WeakPower>(cardPlay.Target, consumed, owner.Creature, card);
		}
		if (card.Rarity == CardRarity.Rare && consumed >= cap)
		{
			await PlayerCmd.GainEnergy(1m, owner);
		}
	}

	public static async Task<CardModel?> ChooseOne(PlayerChoiceContext choiceContext, Player owner, IReadOnlyList<CardModel> options, LocString prompt, bool cancelable = false)
	{
		return await CardSelectCmd.FromChooseACardScreen(choiceContext, options, owner, cancelable);
	}

	internal static async Task RunWithHookContext(Player owner, Func<PlayerChoiceContext, Task> body)
	{
		ulong? netId = LocalContext.NetId;
		if (!netId.HasValue)
		{
			await body(new BlockingPlayerChoiceContext());
			return;
		}
		HookPlayerChoiceContext hookPlayerChoiceContext = new HookPlayerChoiceContext(owner, netId.Value, GameActionType.Combat);
		Task task = body(hookPlayerChoiceContext);
		if (await hookPlayerChoiceContext.AssignTaskAndWaitForPauseOrCompletion(task))
		{
			await task;
		}
	}

	public static int GetEffectiveScryAmount(Player owner, int amount)
	{
		if (amount <= 0)
		{
			return 0;
		}
		if (owner.Relics.Any((RelicModel relic) => relic.Id.Entry == "GOLDEN_EYE"))
		{
			amount += 2;
		}
		if (owner.Creature.HasPower<GuardNextScryPower>())
		{
			amount = Math.Max(0, amount - 2);
		}
		return amount;
	}

	public static async Task<List<CardModel>> Scry(PlayerChoiceContext choiceContext, Player owner, int amount, CardModel? source = null)
	{
		List<CardModel> emptyResult = new List<CardModel>();
		if (amount <= 0)
		{
			return emptyResult;
		}
		CardPile pile = PileType.Draw.GetPile(owner);
		if (pile.Cards.Count == 0)
		{
			await CardPileCmd.ShuffleIfNecessary(choiceContext, owner);
			pile = PileType.Draw.GetPile(owner);
			if (pile.Cards.Count == 0)
			{
				return emptyResult;
			}
		}
		int effectiveScryAmount = GetEffectiveScryAmount(owner, amount);
		List<CardModel> topCards = pile.Cards.Take(effectiveScryAmount).ToList();
		if (topCards.Count == 0)
		{
			return emptyResult;
		}
		GuardNextScryPower guard = owner.Creature.GetPower<GuardNextScryPower>();
		if (guard != null)
		{
			await PowerCmd.Remove(guard);
			List<CardModel> list = topCards.Where((CardModel c) => c.Type == CardType.Attack && c is IProphecyCard).Take(Math.Max(0, guard.Amount)).ToList();
			foreach (CardModel card in list)
			{
				CardPile? pile2 = card.Pile;
				if (pile2 != null && pile2.Type == PileType.Draw)
				{
					if (owner.Creature.IsDead)
					{
						break;
					}
					await CardPileCmd.Add(card, PileType.Play);
					await CardCmd.AutoPlay(choiceContext, card, null);
				}
			}
			await OnScry(owner);
			if (source is IProphecyCard)
			{
				await WatcherProphecy.Trigger(owner, new ProphecyContext
				{
					Source = source,
					CardsDiscarded = 0,
					FromScry = true,
					PeekedCards = topCards
				});
			}
			return topCards;
		}
		CardSelectorPrefs prefs = SetCancelable(new CardSelectorPrefs(ScrySelectionPrompt, 0, topCards.Count), value: true);
		List<CardModel> selectedList = (await CardSelectCmd.FromSimpleGrid(choiceContext, topCards, owner, prefs)).ToList();
		foreach (CardModel item in selectedList)
		{
			await CardPileCmd.Add(item, PileType.Discard);
		}
		foreach (CardModel card in selectedList)
		{
			await OnProphecyCardScryDiscarded(owner, card);
			if (card is IOnScryDiscarded onScryDiscarded)
			{
				await onScryDiscarded.OnScryDiscarded(choiceContext, owner);
			}
		}
		await OnScry(owner);
		if (source is IProphecyCard)
		{
			await WatcherProphecy.Trigger(owner, new ProphecyContext
			{
				Source = source,
				CardsDiscarded = selectedList.Count,
				FromScry = true,
				PeekedCards = topCards
			});
		}
		return selectedList;
	}

	public static async Task<List<CardModel>> ScryAutoDiscard(PlayerChoiceContext choiceContext, Player owner, int amount, Func<IReadOnlyList<CardModel>, IEnumerable<CardModel>> selector, CardModel? source = null)
	{
		List<CardModel> empty = new List<CardModel>();
		if (amount <= 0)
		{
			return empty;
		}
		CardPile pile = PileType.Draw.GetPile(owner);
		if (pile.Cards.Count == 0)
		{
			await CardPileCmd.ShuffleIfNecessary(choiceContext, owner);
			pile = PileType.Draw.GetPile(owner);
			if (pile.Cards.Count == 0)
			{
				return empty;
			}
		}
		int effectiveScryAmount = GetEffectiveScryAmount(owner, amount);
		List<CardModel> topCards = pile.Cards.Take(effectiveScryAmount).ToList();
		if (topCards.Count == 0)
		{
			return empty;
		}
		GuardNextScryPower guard = owner.Creature.GetPower<GuardNextScryPower>();
		if (guard != null)
		{
			await PowerCmd.Remove(guard);
			List<CardModel> list = topCards.Where((CardModel c) => c.Type == CardType.Attack && c is IProphecyCard).Take(Math.Max(0, guard.Amount)).ToList();
			foreach (CardModel card in list)
			{
				CardPile? pile2 = card.Pile;
				if (pile2 != null && pile2.Type == PileType.Draw)
				{
					if (owner.Creature.IsDead)
					{
						break;
					}
					await CardPileCmd.Add(card, PileType.Play);
					await CardCmd.AutoPlay(choiceContext, card, null);
				}
			}
			await OnScry(owner);
			if (source is IProphecyCard)
			{
				await WatcherProphecy.Trigger(owner, new ProphecyContext
				{
					Source = source,
					CardsDiscarded = 0,
					FromScry = true,
					PeekedCards = topCards
				});
			}
			return topCards;
		}
		List<CardModel> discarded = selector(topCards).Distinct().ToList();
		foreach (CardModel item in discarded)
		{
			await CardPileCmd.Add(item, PileType.Discard);
		}
		foreach (CardModel card in discarded)
		{
			await OnProphecyCardScryDiscarded(owner, card);
			if (card is IOnScryDiscarded onScryDiscarded)
			{
				await onScryDiscarded.OnScryDiscarded(choiceContext, owner);
			}
		}
		await OnScry(owner);
		if (source is IProphecyCard)
		{
			await WatcherProphecy.Trigger(owner, new ProphecyContext
			{
				Source = source,
				CardsDiscarded = discarded.Count,
				FromScry = true,
				PeekedCards = topCards
			});
		}
		return discarded;
	}

	public static Task<CardModel> CreateWatcherCard<T>(Player owner) where T : CardModel
	{
		CardModel cardModel = WatcherCreatureCompat.GetCombatState(owner.Creature).CreateCard<T>(owner);
		UpgradeIfMasterReality(owner, cardModel);
		return Task.FromResult(cardModel);
	}

	public static CardModel CreateWatcherCard(Player owner, CardModel canonicalCard)
	{
		CardModel cardModel = WatcherCreatureCompat.GetCombatState(owner.Creature).CreateCard(canonicalCard, owner);
		UpgradeIfMasterReality(owner, cardModel);
		return cardModel;
	}

	private static void UpgradeIfMasterReality(Player owner, CardModel card)
	{
		if (owner.Creature.HasPower<MasterRealityPower>() && card.IsUpgradable)
		{
			CardCmd.Upgrade(card);
		}
	}

	public static async Task TakeExtraTurn(Player owner)
	{
		await WatcherPowerCmdCompat.Apply<WatcherExtraTurnPower>(owner.Creature, 1m, owner.Creature, null);
		await EndTurnSafely(owner);
	}

	internal static async Task EndTurnSafely(Player owner)
	{
		if (owner == null)
		{
			return;
		}
		if ((owner.RunState?.Players.Count ?? 1) <= 1)
		{
			PlayerCmd.EndTurn(owner, canBackOut: false);
			return;
		}
		PlayerCombatState? playerCombatState = owner.PlayerCombatState;
		if (playerCombatState != null && playerCombatState.Phase == PlayerTurnPhase.Play)
		{
			RunManager instance = RunManager.Instance;
			if (instance != null && instance.ActionQueueSynchronizer?.CombatState == ActionSynchronizerCombatState.EndTurnPhaseOne)
			{
				PlayerCmd.EndTurn(owner, canBackOut: false);
				return;
			}
		}
		Divinity power = owner.Creature.GetPower<Divinity>();
		if (power != null)
		{
			await PowerCmd.Remove(power);
			await OnStanceChanged(owner, typeof(Divinity), GetCurrentStance(owner.Creature));
		}
	}

	private static async Task ChangeStance<T>(Player owner, CardModel? source) where T : PowerModel
	{
		if (!owner.Creature.HasPower<CannotChangeStancePower>())
		{
			Type targetStance = typeof(T);
			Type currentStance = GetCurrentStance(owner.Creature);
			if (!(currentStance == targetStance))
			{
				await RemoveAllStances(owner.Creature);
				await WatcherPowerCmdCompat.Apply<T>(owner.Creature, 1m, owner.Creature, source);
				await OnStanceChanged(owner, currentStance, targetStance);
			}
		}
	}

	private static Type? GetCurrentStance(Creature creature)
	{
		if (creature.HasPower<Divinity>())
		{
			return typeof(Divinity);
		}
		if (creature.HasPower<Foreseen>())
		{
			return typeof(Foreseen);
		}
		if (creature.HasPower<Wrath>())
		{
			return typeof(Wrath);
		}
		if (creature.HasPower<Calm>())
		{
			return typeof(Calm);
		}
		return null;
	}

	private static async Task RemoveAllStances(Creature creature)
	{
		await PowerCmd.Remove(creature.GetPower<Divinity>());
		await PowerCmd.Remove(creature.GetPower<Foreseen>());
		await PowerCmd.Remove(creature.GetPower<Wrath>());
		await PowerCmd.Remove(creature.GetPower<Calm>());
	}

	private static async Task OnScry(Player owner)
	{
		if (owner.Creature.HasPower<NirvanaPower>())
		{
			await CreatureCmd.GainBlock(owner.Creature, owner.Creature.GetPowerAmount<NirvanaPower>(), ValueProp.Unpowered, null);
		}
		List<CardModel> list = PileType.Discard.GetPile(owner).Cards.Where((CardModel card) => card is WatcherWeave).ToList();
		foreach (CardModel item in list)
		{
			await CardPileCmd.Add(item, PileType.Hand);
		}
	}

	private static async Task OnStanceChanged(Player owner, Type? oldStance, Type? newStance)
	{
		UpdateEyeAnimation(owner.Creature, newStance);
		if (newStance == typeof(Wrath) && owner.Creature.HasPower<RushdownPower>())
		{
			WatcherAudioHelper.PlayOneShot("res://audio/watcher/mantra.ogg");
			int drawAmount = owner.Creature.GetPowerAmount<RushdownPower>();
			if (!PileType.Play.GetPile(owner).Cards.Any())
			{
				await RunWithHookContext(owner, (PlayerChoiceContext choiceCtx) => CardPileCmd.Draw(choiceCtx, drawAmount, owner));
			}
			else
			{
				owner.Creature.GetPower<RushdownPower>().PendingDraws += drawAmount;
			}
		}
		List<CardModel> list = PileType.Discard.GetPile(owner).Cards.Where((CardModel card) => card is WatcherFlurryOfBlows).ToList();
		foreach (CardModel item in list)
		{
			await CardPileCmd.Add(item, PileType.Hand);
		}
		if (owner.Creature.HasPower<MentalFortressPower>() && oldStance != newStance)
		{
			await CreatureCmd.GainBlock(owner.Creature, owner.Creature.GetPowerAmount<MentalFortressPower>(), ValueProp.Unpowered, null);
		}
		if (oldStance == typeof(Calm) && owner.Relics.Any((RelicModel relic) => relic.Id.Entry == "VIOLET_LOTUS"))
		{
			await PlayerCmd.GainEnergy(1m, owner);
		}
		if (oldStance == typeof(Foreseen) && (owner.Creature.GetPower<WatcherStatePower>()?.KnowFateConsumedThisTurn ?? false))
		{
			await WatcherPowerCmdCompat.Apply<EnlightenFatePower>(owner.Creature, 1m, owner.Creature, null);
		}
	}

	internal static void UpdateEyeAnimation(Creature creature, Type? stance)
	{
		NCreature nCreature = NCombatRoom.Instance?.GetCreatureNode(creature);
		if (nCreature != null)
		{
			string animationName = ((stance == typeof(Divinity)) ? "Divinity" : ((stance == typeof(Wrath)) ? "Wrath" : ((stance == typeof(Calm)) ? "Calm" : "None")));
			WatcherSkeletonHelper.UpdateEyeTop(nCreature.Body, animationName);
		}
	}

	internal static async Task OnProphecyCardScryDiscarded(Player owner, CardModel card)
	{
		if (card is IProphecyCard && card.Owner == owner)
		{
			await WatcherPowerCmdCompat.Apply<KnowFatePower>(owner.Creature, 1m, owner.Creature, card);
		}
	}

	private static async Task<WatcherStatePower?> EnsureState(Player? owner)
	{
		if (owner?.Creature == null || owner.PlayerCombatState == null || !owner.Creature.IsAlive || WatcherCreatureCompat.GetCombatState(owner.Creature) == null)
		{
			return null;
		}
		WatcherStatePower power = owner.Creature.GetPower<WatcherStatePower>();
		if (power != null)
		{
			return power;
		}
		try
		{
			return (await WatcherPowerCmdCompat.Apply<WatcherStatePower>(owner.Creature, 1m, owner.Creature, null)) ?? owner.Creature.GetPower<WatcherStatePower>();
		}
		catch
		{
			return owner.Creature.GetPower<WatcherStatePower>();
		}
	}

	internal static bool IsBlockedByCardLogic(CardModel card)
	{
		if (!card.CanPlay(out UnplayableReason reason, out AbstractModel _))
		{
			return (reason & UnplayableReason.BlockedByCardLogic) != 0;
		}
		return false;
	}

	internal static CardSelectorPrefs SetCancelable(CardSelectorPrefs prefs, bool value)
	{
		object obj = prefs;
		typeof(CardSelectorPrefs).GetProperty("Cancelable", BindingFlags.Instance | BindingFlags.Public).SetValue(obj, value);
		return (CardSelectorPrefs)obj;
	}
}
internal static class WatcherCreatureCompat
{
	private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

	private static readonly PropertyInfo? CreatureCombatStateProperty = typeof(Creature).GetProperty("CombatState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

	private static readonly FieldInfo? CreatureCombatStateBackingField = typeof(Creature).GetField("<CombatState>k__BackingField", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? typeof(Creature).GetField("_combatState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ?? typeof(Creature).GetField("CombatState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

	private static readonly PropertyInfo? CombatManagerInstanceProperty = typeof(CombatManager).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);

	private static readonly MethodInfo? CombatManagerDebugOnlyGetStateMethod = typeof(CombatManager).GetMethod("DebugOnlyGetState", BindingFlags.Instance | BindingFlags.Public);

	public static CombatState? GetCombatState(Creature? creature)
	{
		if (creature == null)
		{
			return null;
		}
		if (TryReadCombatState(() => CreatureCombatStateProperty?.GetValue(creature), out CombatState combat))
		{
			return combat;
		}
		if (TryReadCombatState(() => CreatureCombatStateBackingField?.GetValue(creature), out combat))
		{
			return combat;
		}
		combat = GetActiveCombatState();
		if (combat != null && ContainsCreature(combat, creature))
		{
			return combat;
		}
		return null;
	}

	public static CombatState RequireCombatState(Creature? creature, string message)
	{
		return GetCombatState(creature) ?? throw new InvalidOperationException(message);
	}

	public static int GetPlayerTurnCounter(Creature? creature)
	{
		int? num = creature?.Player?.PlayerCombatState?.TurnNumber;
		if (num.HasValue)
		{
			return num.Value;
		}
		return GetCombatState(creature)?.RoundNumber ?? 0;
	}

	private static CombatState? GetActiveCombatState()
	{
		if (!TryReadCombatState(delegate
		{
			object obj = CombatManagerInstanceProperty?.GetValue(null);
			return (obj != null) ? CombatManagerDebugOnlyGetStateMethod?.Invoke(obj, null) : null;
		}, out CombatState combat))
		{
			return null;
		}
		return combat;
	}

	private static bool TryReadCombatState(Func<object?> read, out CombatState? combat)
	{
		combat = null;
		try
		{
			combat = read() as CombatState;
			return combat != null;
		}
		catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is MissingMethodException || ex is TargetInvocationException)
		{
			return false;
		}
	}

	private static bool ContainsCreature(CombatState combat, Creature creature)
	{
		try
		{
			return combat.Creatures.Contains(creature);
		}
		catch (Exception ex) when (ex is MissingMethodException || ex is InvalidOperationException || ex is TargetInvocationException)
		{
			return false;
		}
	}
}
internal static class WatcherDialogueHelper
{
	private static readonly string WatcherEntry = ModelDb.GetId(typeof(Watcher)).Entry;

	public static void InjectWatcherDialogue(AncientDialogueSet dialogueSet, string ancientEntry, IReadOnlyList<AncientDialogue> dialogues)
	{
		if (dialogueSet.CharacterDialogues.ContainsKey(WatcherEntry))
		{
			return;
		}
		dialogueSet.CharacterDialogues[WatcherEntry] = dialogues;
		for (int i = 0; i < dialogues.Count; i++)
		{
			dialogues[i].PopulateLines(ancientEntry, WatcherEntry, i);
			IReadOnlyList<AncientDialogueLine> lines = dialogues[i].Lines;
			for (int j = 0; j < lines.Count - 1; j++)
			{
				AncientDialogueLine ancientDialogueLine = lines[j];
				if (ancientDialogueLine.LineText != null)
				{
					string locEntryKey = ancientDialogueLine.LineText.LocEntryKey;
					string text = locEntryKey.Substring(0, locEntryKey.LastIndexOf('.'));
					ancientDialogueLine.NextButtonText = new LocString("ancients", text + ".next");
				}
			}
		}
	}

	public static AncientDialogue Lines(int lineCount, int? visitIndex = null)
	{
		string[] array = new string[lineCount];
		System.Array.Fill(array, "");
		return new AncientDialogue(array)
		{
			VisitIndex = visitIndex
		};
	}

	public static AncientDialogue ArchitectLines(int lineCount, int? visitIndex = null, ArchitectAttackers endAttackers = ArchitectAttackers.Both, ArchitectAttackers startAttackers = ArchitectAttackers.None)
	{
		string[] array = new string[lineCount];
		System.Array.Fill(array, "");
		return new AncientDialogue(array)
		{
			VisitIndex = visitIndex,
			StartAttackers = startAttackers,
			EndAttackers = endAttackers
		};
	}
}
[HarmonyPatch(typeof(AncientDialogueSet), "PopulateLocKeys")]
internal static class WatcherAncientDialoguePatch
{
	private static void Postfix(AncientDialogueSet __instance, string ancientEntry)
	{
		IReadOnlyList<AncientDialogue> dialoguesForAncient = GetDialoguesForAncient(ancientEntry);
		if (dialoguesForAncient != null)
		{
			try
			{
				WatcherDialogueHelper.InjectWatcherDialogue(__instance, ancientEntry, dialoguesForAncient);
			}
			catch (Exception value)
			{
				GD.PrintErr($"[Watcher] Dialogue injection failed for {ancientEntry}: {value}");
			}
		}
	}

	private static IReadOnlyList<AncientDialogue>? GetDialoguesForAncient(string entry)
	{
		return entry switch
		{
			"NEOW" => new AncientDialogue[3]
			{
				WatcherDialogueHelper.Lines(1, 0),
				WatcherDialogueHelper.Lines(1, 1),
				WatcherDialogueHelper.Lines(1, 4)
			}, 
			"NONUPEIPE" => new AncientDialogue[3]
			{
				WatcherDialogueHelper.Lines(1, 0),
				WatcherDialogueHelper.Lines(1, 1),
				WatcherDialogueHelper.Lines(1, 4)
			}, 
			"DARV" => new AncientDialogue[3]
			{
				WatcherDialogueHelper.Lines(1, 0),
				WatcherDialogueHelper.Lines(1, 1),
				WatcherDialogueHelper.Lines(1, 4)
			}, 
			"VAKUU" => new AncientDialogue[3]
			{
				WatcherDialogueHelper.Lines(1, 0),
				WatcherDialogueHelper.Lines(1, 1),
				WatcherDialogueHelper.Lines(1, 4)
			}, 
			"PAEL" => new AncientDialogue[3]
			{
				WatcherDialogueHelper.Lines(1, 0),
				WatcherDialogueHelper.Lines(1, 1),
				WatcherDialogueHelper.Lines(1, 4)
			}, 
			"OROBAS" => new AncientDialogue[3]
			{
				WatcherDialogueHelper.Lines(1, 0),
				WatcherDialogueHelper.Lines(1, 1),
				WatcherDialogueHelper.Lines(1, 4)
			}, 
			"TANX" => new AncientDialogue[3]
			{
				WatcherDialogueHelper.Lines(1, 0),
				WatcherDialogueHelper.Lines(1, 1),
				WatcherDialogueHelper.Lines(1, 4)
			}, 
			"TEZCATARA" => new AncientDialogue[3]
			{
				WatcherDialogueHelper.Lines(1, 0),
				WatcherDialogueHelper.Lines(1, 1),
				WatcherDialogueHelper.Lines(1, 4)
			}, 
			"THE_ARCHITECT" => new AncientDialogue[3]
			{
				WatcherDialogueHelper.ArchitectLines(1, 0),
				WatcherDialogueHelper.ArchitectLines(1, 1),
				WatcherDialogueHelper.ArchitectLines(1, 2)
			}, 
			_ => null, 
		};
	}
}
public static class WatcherEnchantStack
{
	[CompilerGenerated]

	private static readonly object _sentinel = new object();

	private static readonly ConditionalWeakTable<CardModel, List<EnchantmentModel>> _extras = new ConditionalWeakTable<CardModel, List<EnchantmentModel>>();

	private static readonly ConditionalWeakTable<EnchantmentModel, object> _temp = new ConditionalWeakTable<EnchantmentModel, object>();

	private static readonly HashSet<string> _excludedFromRandomPool = new HashSet<string>
	{
		"Goopy", "Steady", "RoyallyApproved", "TezcatarasEmber", "SoulsPower", "Adroit", "Corrupted", "Swift", "Sown", "Momentum",
		"Inky", "Clone", "Imbued"
	};

	private static List<EnchantmentModel>? _cachedRandomPool;

	private static bool _subscribedCombatEnded;

	public static IReadOnlyList<EnchantmentModel> RandomPool
	{
		get
		{
			if (_cachedRandomPool != null)
			{
				return _cachedRandomPool;
			}
			_cachedRandomPool = (from e in ModelDb.DebugEnchantments
				where !(e is DeprecatedEnchantment)
				where !_excludedFromRandomPool.Contains(e.GetType().Name)
				where !HasOnEnchantOverride(e.GetType())
				where !HasOnPlayOverride(e.GetType())
				select e).ToList();
			return _cachedRandomPool;
		}
	}

	public static List<EnchantmentModel>? GetExtras(CardModel card)
	{
		if (!_extras.TryGetValue(card, out List<EnchantmentModel> value))
		{
			return null;
		}
		return value;
	}

	public static IEnumerable<EnchantmentModel> AllEnchantments(CardModel card)
	{
		if (card.Enchantment != null)
		{
			yield return card.Enchantment;
		}
		if (_extras.TryGetValue(card, out List<EnchantmentModel> extras))
		{
			foreach (EnchantmentModel enchantment in extras)
			{
				yield return enchantment;
			}
		}
	}

	public static bool IsTemp(EnchantmentModel e)
	{
		object value;
		return _temp.TryGetValue(e, out value);
	}

	private static bool HasOnEnchantOverride(Type t)
	{
		MethodInfo method = t.GetMethod("OnEnchant", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		if (method != null)
		{
			return method.DeclaringType != typeof(EnchantmentModel);
		}
		return false;
	}

	private static bool HasOnPlayOverride(Type t)
	{
		MethodInfo method = t.GetMethod("OnPlay", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[2]
		{
			typeof(PlayerChoiceContext),
			typeof(CardPlay)
		}, null);
		if (method != null)
		{
			return method.DeclaringType != typeof(EnchantmentModel);
		}
		return false;
	}

	public static bool CanApplyTempEnchantmentTo(CardModel card, EnchantmentModel canonical, bool directed = false)
	{
		return CanApplyAsTempStack(canonical, card, directed);
	}

	public static EnchantmentModel? ApplyTempEnchantment(CardModel card, EnchantmentModel canonical, decimal amount = 1m)
	{
		if (!CanApplyAsTempStack(canonical, card, directed: false))
		{
			return null;
		}
		EnchantmentModel enchantmentModel;
		try
		{
			enchantmentModel = canonical.ToMutable();
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] Failed to clone enchantment " + canonical.GetType().Name + ": " + ex.Message);
			return null;
		}
		try
		{
			if (card.Enchantment == null)
			{
				card.EnchantInternal(enchantmentModel, amount);
				try
				{
					enchantmentModel.ModifyCard();
				}
				catch
				{
					try
					{
						card.ClearEnchantmentInternal();
					}
					catch
					{
					}
					throw;
				}
			}
			else
			{
				enchantmentModel.ApplyInternal(card, amount);
				try
				{
					enchantmentModel.ModifyCard();
				}
				catch
				{
					try
					{
						enchantmentModel.ClearInternal();
					}
					catch
					{
					}
					throw;
				}
				_extras.GetValue(card, (CardModel _) => new List<EnchantmentModel>()).Add(enchantmentModel);
			}
			_temp.Add(enchantmentModel, _sentinel);
			return enchantmentModel;
		}
		catch (Exception ex2)
		{
			Log.Error($"[Watcher] Failed to apply temp enchantment {canonical.GetType().Name} to {card.Id}: {ex2.Message}");
			return null;
		}
	}

	private static bool CanApplyAsTempStack(EnchantmentModel canonical, CardModel card, bool directed)
	{
		CardType type = card.Type;
		if ((uint)(type - 4) <= 2u)
		{
			return false;
		}
		if (!canonical.CanEnchantCardType(type))
		{
			return false;
		}
		CardPile? pile = card.Pile;
		if (pile != null && pile.Type == PileType.Deck && card.Keywords.Contains(CardKeyword.Unplayable))
		{
			return false;
		}
		if (directed && !HasDirectedFit(canonical, card))
		{
			return false;
		}
		Type canonicalType = canonical.GetType();
		if (card.Enchantment?.GetType() == canonicalType)
		{
			return false;
		}
		List<EnchantmentModel> extras = GetExtras(card);
		if (extras != null && extras.Any((EnchantmentModel e) => e.GetType() == canonicalType))
		{
			return false;
		}
		return true;
	}

	private static bool HasDirectedFit(EnchantmentModel canonical, CardModel card)
	{
		Type type = canonical.GetType();
		if (Overrides(type, "EnchantBlockAdditive", typeof(decimal)) || Overrides(type, "EnchantBlockMultiplicative", typeof(decimal)))
		{
			if (card.Type != CardType.Attack)
			{
				return card.GainsBlock;
			}
			return false;
		}
		string name = type.Name;
		if (!(name == "Slither"))
		{
			if (name == "Spiral")
			{
				return card.Rarity == CardRarity.Basic;
			}
			return true;
		}
		return !card.Keywords.Contains(CardKeyword.Unplayable);
	}

	private static bool Overrides(Type t, string methodName, params Type[] parameterTypes)
	{
		MethodInfo method = t.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, parameterTypes, null);
		if (method != null)
		{
			return method.DeclaringType != typeof(EnchantmentModel);
		}
		return false;
	}

	public static void ClearAllTemp(CombatState? combatState)
	{
		if (combatState == null)
		{
			return;
		}
		foreach (Player player in combatState.Players)
		{
			if (player.PlayerCombatState == null)
			{
				continue;
			}
			foreach (CardPile allPile in player.PlayerCombatState.AllPiles)
			{
				foreach (CardModel item in allPile.Cards.ToList())
				{
					ClearTempForCard(item);
				}
			}
		}
	}

	public static void ClearTempForCard(CardModel card)
	{
		if (_extras.TryGetValue(card, out List<EnchantmentModel> value))
		{
			for (int num = value.Count - 1; num >= 0; num--)
			{
				if (IsTemp(value[num]))
				{
					try
					{
						value[num].ClearInternal();
					}
					catch
					{
					}
					_temp.Remove(value[num]);
					value.RemoveAt(num);
				}
			}
			if (value.Count == 0)
			{
				_extras.Remove(card);
			}
		}
		if (card.Enchantment == null || !IsTemp(card.Enchantment))
		{
			return;
		}
		EnchantmentModel enchantment = card.Enchantment;
		_temp.Remove(enchantment);
		try
		{
			card.ClearEnchantmentInternal();
		}
		catch
		{
		}
		if (_extras.TryGetValue(card, out List<EnchantmentModel> value2) && value2.Count > 0)
		{
			EnchantmentModel enchantmentModel = value2[0];
			value2.RemoveAt(0);
			if (value2.Count == 0)
			{
				_extras.Remove(card);
			}
			try
			{
				int amount = enchantmentModel.Amount;
				enchantmentModel.ClearInternal();
				card.EnchantInternal(enchantmentModel, amount);
				enchantmentModel.ModifyCard();
			}
			catch (Exception ex)
			{
				Log.Error("[Watcher] Failed to promote extra enchantment: " + ex.Message);
			}
		}
	}

	public static void RegisterSubscriptions()
	{
		ModHelper.SubscribeForCombatStateHooks("watcher_enchant_extras", GetCombatHookSubscribers);
		if (!_subscribedCombatEnded)
		{
			_subscribedCombatEnded = true;
			CombatManager.Instance.CombatEnded += OnCombatEnded;
		}
	}

	private static IEnumerable<AbstractModel> GetCombatHookSubscribers(object? _)
	{
		WatcherEnchantStackHookProxy instance = WatcherEnchantStackHookProxy.Instance;
		if (instance == null)
		{
			return System.Array.Empty<AbstractModel>();
		}
		return new AbstractModel[1] { instance };
	}

	private static void OnCombatEnded(CombatRoom _)
	{
		try
		{
			ClearAllTemp(CombatManager.Instance.DebugOnlyGetState());
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] Temp enchantment cleanup failed: " + ex.Message);
		}
	}
}
public sealed class WatcherEnchantStackHookProxy : AbstractModel
{
	public static WatcherEnchantStackHookProxy? Instance { get; private set; }

	public override bool ShouldReceiveCombatHooks => true;

	public WatcherEnchantStackHookProxy()
	{
		Instance = this;
	}

	public override decimal ModifyBlockAdditive(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		if (cardSource == null)
		{
			return 0m;
		}
		List<EnchantmentModel> extras = WatcherEnchantStack.GetExtras(cardSource);
		if (extras == null || extras.Count == 0)
		{
			return 0m;
		}
		decimal num = block;
		foreach (EnchantmentModel item in extras)
		{
			num += item.EnchantBlockAdditive(num);
			num *= item.EnchantBlockMultiplicative(num);
		}
		return num - block;
	}

	public decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
	{
		if (cardSource == null)
		{
			return 0m;
		}
		List<EnchantmentModel> extras = WatcherEnchantStack.GetExtras(cardSource);
		if (extras == null || extras.Count == 0)
		{
			return 0m;
		}
		decimal num = amount;
		foreach (EnchantmentModel item in extras)
		{
			num += item.EnchantDamageAdditive(num, props);
			num *= item.EnchantDamageMultiplicative(num, props);
		}
		return num - amount;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "Reported native SIGSEGV during Harmony.Patch on ARM64 (neighbour-skip)")]
[HarmonyPatch(typeof(BlockVar), "UpdateCardPreview")]
internal static class WatcherBlockVarEnchantExtrasPatch
{
	private static void Postfix(BlockVar __instance, CardModel card, bool runGlobalHooks)
	{
		if (runGlobalHooks)
		{
			return;
		}
		List<EnchantmentModel> extras = WatcherEnchantStack.GetExtras(card);
		if (extras == null || extras.Count == 0)
		{
			return;
		}
		decimal previewValue = __instance.PreviewValue;
		_ = __instance.Props;
		foreach (EnchantmentModel item in extras)
		{
			previewValue += item.EnchantBlockAdditive(previewValue);
			previewValue *= item.EnchantBlockMultiplicative(previewValue);
		}
		if (!card.IsEnchantmentPreview)
		{
			__instance.EnchantedValue = previewValue;
		}
		__instance.PreviewValue = previewValue;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "Mirror BlockVar; same neighbour-skip class on ARM64")]
[HarmonyPatch(typeof(DamageVar), "UpdateCardPreview")]
internal static class WatcherDamageVarEnchantExtrasPatch
{
	private static void Postfix(DamageVar __instance, CardModel card, bool runGlobalHooks)
	{
		if (runGlobalHooks)
		{
			return;
		}
		List<EnchantmentModel> extras = WatcherEnchantStack.GetExtras(card);
		if (extras == null || extras.Count == 0)
		{
			return;
		}
		decimal previewValue = __instance.PreviewValue;
		ValueProp props = __instance.Props;
		foreach (EnchantmentModel item in extras)
		{
			previewValue += item.EnchantDamageAdditive(previewValue, props);
			previewValue *= item.EnchantDamageMultiplicative(previewValue, props);
		}
		if (!card.IsEnchantmentPreview)
		{
			__instance.EnchantedValue = previewValue;
		}
		__instance.PreviewValue = previewValue;
	}
}
[HarmonyPatch(typeof(CardModel), "GetEnchantedReplayCount")]
internal static class WatcherCardEnchantedReplayCountExtrasPatch
{
	private static void Postfix(CardModel __instance, ref int __result)
	{
		List<EnchantmentModel> extras = WatcherEnchantStack.GetExtras(__instance);
		if (extras == null || extras.Count == 0)
		{
			return;
		}
		int num = __result;
		foreach (EnchantmentModel item in extras)
		{
			num = item.EnchantPlayCount(num);
		}
		__result = num;
	}
}
internal static class WatcherProphecyDescriptionKeyword
{
	internal static void Add(CardModel card, ref string description)
	{
		if (card is IProphecyCard)
		{
			string text = "[gold]" + new LocString("powers", "PROPHECY.title").GetFormattedText() + "[/gold].";
			if (!(description == text) && !description.StartsWith(text + "\n", StringComparison.Ordinal))
			{
				description = text + "\n" + description;
			}
		}
	}
}
[HarmonyPatch(typeof(CardModel), "GetDescriptionForPile", new Type[]
{
	typeof(PileType),
	typeof(Creature)
})]
internal static class WatcherProphecyDescriptionForPilePatch
{
	private static void Postfix(CardModel __instance, ref string __result)
	{
		WatcherProphecyDescriptionKeyword.Add(__instance, ref __result);
	}
}
[HarmonyPatch(typeof(CardModel), "GetDescriptionForUpgradePreview")]
internal static class WatcherProphecyUpgradeDescriptionPatch
{
	private static void Postfix(CardModel __instance, ref string __result)
	{
		WatcherProphecyDescriptionKeyword.Add(__instance, ref __result);
	}
}
[HarmonyPatch(typeof(CardModel), "HoverTips", MethodType.Getter)]
internal static class WatcherCardHoverTipsExtrasPatch
{
	private static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
	{
		List<EnchantmentModel> extras = WatcherEnchantStack.GetExtras(__instance);
		bool flag = extras != null && extras.Count > 0;
		bool flag2 = __instance is IProphecyCard;
		bool flag3 = NeedsScryTip(__instance);
		bool flag4 = flag2 || NeedsFinalityTip(__instance);
		bool flag5 = NeedsFullTip(__instance);
		bool flag6 = NeedsAwakenFateTip(__instance);
		bool flag7 = NeedsConfusionTip(__instance);
		bool flag8 = NeedsKnowFateTip(__instance);
		bool flag9 = NeedsStanceTip(__instance);
		bool flag10 = NeedsCalmTip(__instance);
		bool flag11 = NeedsWrathTip(__instance);
		bool flag12 = NeedsDivinityTip(__instance);
		bool flag13 = NeedsForeseenTip(__instance);
		bool flag14 = NeedsMantraTip(__instance);
		bool flag15 = NeedsEnchantmentTip(__instance);
		bool flag16 = NeedsDirectedTip(__instance);
		if (!flag && !flag2 && !flag3 && !flag4 && !flag5 && !flag6 && !flag7 && !flag8 && !flag9 && !flag10 && !flag11 && !flag12 && !flag13 && !flag14 && !flag15 && !flag16)
		{
			return;
		}
		List<IHoverTip> list = __result.ToList();
		if (flag2 && !ContainsHoverTip(list, WatcherHoverTips.Prophecy))
		{
			list.Insert(0, WatcherHoverTips.Prophecy);
		}
		if (flag3 && !ContainsHoverTip(list, WatcherHoverTips.Scry))
		{
			list.Add(WatcherHoverTips.Scry);
		}
		if (flag7 && !ContainsHoverTip(list, WatcherHoverTips.Confusion))
		{
			list.Add(WatcherHoverTips.Confusion);
		}
		if (flag4 && !ContainsHoverTip(list, WatcherHoverTips.Finality))
		{
			list.Add(WatcherHoverTips.Finality);
		}
		if (flag5 && !ContainsHoverTip(list, WatcherHoverTips.Full))
		{
			list.Add(WatcherHoverTips.Full);
		}
		if (flag6)
		{
			IHoverTip hoverTip = HoverTipFactory.FromPower<EnlightenFatePower>(null);
			if (!ContainsHoverTip(list, hoverTip))
			{
				list.Add(hoverTip);
			}
		}
		if (flag8)
		{
			AddHoverTipIfMissing(list, HoverTipFactory.FromPower<KnowFatePower>(null));
		}
		if (flag9 && !ContainsHoverTip(list, WatcherHoverTips.Stance))
		{
			list.Add(WatcherHoverTips.Stance);
		}
		if (flag10)
		{
			AddHoverTipIfMissing(list, HoverTipFactory.FromPower<Calm>(null));
		}
		if (flag11)
		{
			AddHoverTipIfMissing(list, HoverTipFactory.FromPower<Wrath>(null));
		}
		if (flag12)
		{
			AddHoverTipIfMissing(list, HoverTipFactory.FromPower<Divinity>(null));
		}
		if (flag13)
		{
			AddHoverTipIfMissing(list, HoverTipFactory.FromPower<Foreseen>(null));
		}
		if (flag14)
		{
			AddHoverTipIfMissing(list, HoverTipFactory.FromPower<Mantra>(null));
		}
		if (flag15 && !ContainsHoverTip(list, WatcherHoverTips.Enchantment))
		{
			list.Add(WatcherHoverTips.Enchantment);
		}
		if (flag16 && !ContainsHoverTip(list, WatcherHoverTips.Directed))
		{
			list.Add(WatcherHoverTips.Directed);
		}
		if (flag)
		{
			foreach (EnchantmentModel item in extras)
			{
				list.AddRange(item.HoverTips);
			}
		}
		__result = list;
	}

	private static bool ContainsHoverTip(IEnumerable<IHoverTip> list, IHoverTip tip)
	{
		return list.Any((IHoverTip existing) => existing.Id == tip.Id);
	}

	private static void AddHoverTipIfMissing(List<IHoverTip> list, IHoverTip tip)
	{
		if (!ContainsHoverTip(list, tip))
		{
			list.Add(tip);
		}
	}

	private static string RawDescription(CardModel card)
	{
		try
		{
			return card.Description?.GetRawText() ?? string.Empty;
		}
		catch
		{
			return string.Empty;
		}
	}

	private static bool HasAny(CardModel card, params string[] terms)
	{
		string @object = RawDescription(card);
		return terms.Any(@object.Contains);
	}

	private static bool NeedsScryTip(CardModel card)
	{
		return HasAny(card, "预见", "預見", "Scry");
	}

	private static bool NeedsConfusionTip(CardModel card)
	{
		return HasAny(card, "错乱", "錯亂", "Confusion");
	}

	private static bool NeedsFinalityTip(CardModel card)
	{
		return HasAny(card, "终命", "終命", "Finality");
	}

	private static bool NeedsFullTip(CardModel card)
	{
		return HasAny(card, "满额", "滿額", "Full:", "[gold]Full[/gold]");
	}

	private static bool NeedsAwakenFateTip(CardModel card)
	{
		return HasAny(card, "悟命", "Awaken Fate");
	}

	private static bool NeedsKnowFateTip(CardModel card)
	{
		return HasAny(card, "知天命", "Know Fate", "KnowFate");
	}

	private static bool NeedsStanceTip(CardModel card)
	{
		return HasAny(card, "姿态", "姿態", "Stance", "Stances");
	}

	private static bool NeedsCalmTip(CardModel card)
	{
		return HasAny(card, "平静", "平靜", "Calm");
	}

	private static bool NeedsWrathTip(CardModel card)
	{
		return HasAny(card, "愤怒", "憤怒", "Wrath");
	}

	private static bool NeedsDivinityTip(CardModel card)
	{
		return HasAny(card, "神格", "Divinity");
	}

	private static bool NeedsForeseenTip(CardModel card)
	{
		return HasAny(card, "观命", "觀命", "Foreseen");
	}

	private static bool NeedsMantraTip(CardModel card)
	{
		return HasAny(card, "真言", "Mantra");
	}

	private static bool NeedsEnchantmentTip(CardModel card)
	{
		return HasAny(card, "附魔", "Enchant", "Enchantment", "Enchantments");
	}

	private static bool NeedsDirectedTip(CardModel card)
	{
		return HasAny(card, "指向", "Directed");
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "NCard.UpdateEnchantmentVisuals is private — Cecil DMD landmine on ARM64")]
[HarmonyPatch(typeof(NCard), "UpdateEnchantmentVisuals")]
internal static class WatcherNCardExtraEnchantTabsPatch
{
	private static readonly FieldInfo TabRef = WatcherFieldAccess.Field(typeof(NCard), "_enchantmentTab");

	private static readonly ConditionalWeakTable<NCard, List<Control>> _spawned = new ConditionalWeakTable<NCard, List<Control>>();

	private const float _stackOffset = 50f;

	private static void Postfix(NCard __instance)
	{
		try
		{
			Control control = (Control)TabRef.GetValue(__instance);
			CardModel model = __instance.Model;
			if (control == null || model == null)
			{
				return;
			}
			List<Control> value = _spawned.GetValue(__instance, (NCard _) => new List<Control>());
			foreach (Control item in value)
			{
				if (GodotObject.IsInstanceValid(item))
				{
					item.QueueFree();
				}
			}
			value.Clear();
			List<EnchantmentModel> extras = WatcherEnchantStack.GetExtras(model);
			if (extras == null || extras.Count == 0)
			{
				return;
			}
			Node parent = control.GetParent();
			if (parent == null)
			{
				return;
			}
			Vector2 position = control.Position;
			for (int i = 0; i < extras.Count; i++)
			{
				EnchantmentModel enchantmentModel = extras[i];
				Control control2 = (Control)control.Duplicate(2);
				if (control2 != null)
				{
					control2.Visible = true;
					control2.Position = position + Vector2.Down * (50f * (float)(i + 1));
					TextureRect nodeOrNull = control2.GetNodeOrNull<TextureRect>("Icon");
					if (nodeOrNull != null)
					{
						nodeOrNull.Texture = enchantmentModel.Icon;
					}
					Control nodeOrNull2 = control2.GetNodeOrNull<Control>("Label");
					if (nodeOrNull2 != null)
					{
						nodeOrNull2.Visible = enchantmentModel.ShowAmount;
					}
					parent.AddChild(control2, forceReadableName: false, Node.InternalMode.Disabled);
					value.Add(control2);
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] Extra enchant tab render failed: " + ex.Message);
		}
	}
}
internal static class WatcherHookCompat
{
	private static readonly MethodInfo? ModifyDamageMethod = FindModifyDamageMethod();

	private static readonly MethodInfo? ShouldFlushMethod = FindShouldFlushMethod();

	internal static IEnumerable<MethodBase> FindHookMethods(string methodName)
	{
		return from method in typeof(Hook).GetMethods(BindingFlags.Static | BindingFlags.Public)
			where method.Name == methodName && typeof(Task).IsAssignableFrom(method.ReturnType)
			select method;
	}

	internal static bool HasHookMethod(string methodName)
	{
		return FindHookMethods(methodName).Any();
	}

	internal static async Task ContinueWith(Task original, Func<Task> continuation)
	{
		await original;
		await continuation();
	}

	internal static IReadOnlyList<AbstractModel> GetHookListeners(object? combatState)
	{
		return ToTypedList<AbstractModel>(combatState?.GetType().GetMethod("IterateHookListeners", BindingFlags.Instance | BindingFlags.Public)?.Invoke(combatState, null));
	}

	internal static IReadOnlyList<Creature> GetEnemies(object? combatState)
	{
		return ToTypedList<Creature>(combatState?.GetType().GetProperty("Enemies", BindingFlags.Instance | BindingFlags.Public)?.GetValue(combatState));
	}

	internal static IReadOnlyList<Creature> GetPlayerCreatures(object? combatState)
	{
		IReadOnlyList<Creature> readOnlyList = ToTypedList<Creature>(combatState?.GetType().GetProperty("PlayerCreatures", BindingFlags.Instance | BindingFlags.Public)?.GetValue(combatState));
		if (readOnlyList.Count > 0)
		{
			return readOnlyList;
		}
		return (from player in ToTypedList<Player>(combatState?.GetType().GetProperty("Players", BindingFlags.Instance | BindingFlags.Public)?.GetValue(combatState))
			select player.Creature into creature
			where creature != null
			select creature).ToList();
	}

	internal static decimal ModifyDamage(IRunState runState, object? combatState, Creature? target, Creature? dealer, decimal damage, ValueProp props, CardModel? cardSource, ModifyDamageHookType modifyDamageHookType, CardPreviewMode previewMode, out IEnumerable<AbstractModel> modifiers)
	{
		modifiers = System.Array.Empty<AbstractModel>();
		if (ModifyDamageMethod == null)
		{
			return damage;
		}
		ParameterInfo[] parameters = ModifyDamageMethod.GetParameters();
		object[] array = ((parameters.Length != 11) ? new object[10]
		{
			runState,
			ToCompatibleCombatState(combatState, parameters[1].ParameterType),
			target,
			dealer,
			damage,
			props,
			cardSource,
			modifyDamageHookType,
			previewMode,
			null
		} : new object[11]
		{
			runState,
			ToCompatibleCombatState(combatState, parameters[1].ParameterType),
			target,
			dealer,
			damage,
			props,
			cardSource,
			null,
			modifyDamageHookType,
			previewMode,
			null
		});
		int num = array.Length - 1;
		try
		{
			object obj = ModifyDamageMethod.Invoke(null, array);
			modifiers = ToTypedList<AbstractModel>(array[num]);
			return (obj is decimal num2) ? num2 : damage;
		}
		catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is MissingMethodException || ex is TargetInvocationException)
		{
			modifiers = System.Array.Empty<AbstractModel>();
			return damage;
		}
	}

	internal static int ModifyXValue(object? combatState, CardModel card, int originalValue)
	{
		if (combatState == null)
		{
			return originalValue;
		}
		try
		{
			return Hook.ModifyXValue((CombatState)combatState, card, originalValue);
		}
		catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is MissingMethodException || ex is TargetInvocationException)
		{
			return originalValue;
		}
	}

	internal static bool ShouldFlush(object? combatState, Player player)
	{
		if (combatState == null || ShouldFlushMethod == null)
		{
			return true;
		}
		ParameterInfo[] parameters = ShouldFlushMethod.GetParameters();
		object[] parameters2 = new object[2]
		{
			ToCompatibleCombatState(combatState, parameters[0].ParameterType),
			player
		};
		try
		{
			object obj = ShouldFlushMethod.Invoke(null, parameters2);
			bool flag = default(bool);
			int num;
			if (obj is bool)
			{
				flag = (bool)obj;
				num = ((1 == 0) ? 1 : 0);
			}
			else
			{
				num = 1;
			}
			return (byte)((uint)num | (flag ? 1u : 0u)) != 0;
		}
		catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is MissingMethodException || ex is TargetInvocationException)
		{
			return true;
		}
	}

	private static MethodInfo? FindModifyDamageMethod()
	{
		return typeof(Hook).GetMethods(BindingFlags.Static | BindingFlags.Public).FirstOrDefault(delegate(MethodInfo method)
		{
			ParameterInfo[] parameters = method.GetParameters();
			if (method.Name != "ModifyDamage" || method.ReturnType != typeof(decimal) || (parameters.Length != 10 && parameters.Length != 11))
			{
				return false;
			}
			int num = parameters.Length - 10;
			return parameters[0].ParameterType == typeof(IRunState) && IsCombatStateParameter(parameters[1].ParameterType) && IsParameterCompatible<Creature>(parameters[2]) && IsParameterCompatible<Creature>(parameters[3]) && parameters[4].ParameterType == typeof(decimal) && parameters[5].ParameterType == typeof(ValueProp) && IsParameterCompatible<CardModel>(parameters[6]) && parameters[7 + num].ParameterType == typeof(ModifyDamageHookType) && parameters[8 + num].ParameterType == typeof(CardPreviewMode) && parameters[9 + num].IsOut;
		});
	}

	private static bool IsParameterCompatible<T>(ParameterInfo parameter)
	{
		Type type = Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
		if (!(type == typeof(T)))
		{
			return type.IsAssignableFrom(typeof(T));
		}
		return true;
	}

	private static MethodInfo? FindShouldFlushMethod()
	{
		return typeof(Hook).GetMethods(BindingFlags.Static | BindingFlags.Public).FirstOrDefault(delegate(MethodInfo method)
		{
			ParameterInfo[] parameters = method.GetParameters();
			return method.Name == "ShouldFlush" && method.ReturnType == typeof(bool) && parameters.Length == 2 && IsCombatStateParameter(parameters[0].ParameterType) && parameters[1].ParameterType == typeof(Player);
		});
	}

	internal static bool IsCombatStateParameter(Type parameterType)
	{
		bool flag = parameterType == typeof(CombatState) || parameterType.FullName == "MegaCrit.Sts2.Core.Combat.ICombatState";
		if (!flag)
		{
			string name = parameterType.Name;
			bool flag2 = ((name == "CombatState" || name == "ICombatState") ? true : false);
			flag = flag2;
		}
		return flag;
	}

	internal static object? ToCompatibleCombatState(object? combatState, Type parameterType)
	{
		if (combatState == null || parameterType.IsInstanceOfType(combatState))
		{
			return combatState;
		}
		return null;
	}

	private static IReadOnlyList<T> ToTypedList<T>(object? value)
	{
		if (value is IEnumerable<T> source)
		{
			return source.ToList();
		}
		if (value is IEnumerable source2)
		{
			return source2.OfType<T>().ToList();
		}
		return System.Array.Empty<T>();
	}
}
[HarmonyPatch]
internal static class WatcherBeforeHandDrawCompatPatch
{
	private static bool Prepare()
	{
		return WatcherHookCompat.HasHookMethod("BeforeHandDraw");
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		return WatcherHookCompat.FindHookMethods("BeforeHandDraw");
	}

	private static void Postfix(ref Task __result, object combatState, Player player, PlayerChoiceContext playerChoiceContext)
	{
		__result = WatcherHookCompat.ContinueWith(__result, async delegate
		{
			foreach (AbstractModel model in WatcherHookCompat.GetHookListeners(combatState))
			{
				if (model is ForesightPower foresightPower)
				{
					playerChoiceContext.PushModel(model);
					try
					{
						await foresightPower.BeforeHandDrawCompat(player, playerChoiceContext);
						model.InvokeExecutionFinished();
					}
					finally
					{
						playerChoiceContext.PopModel(model);
					}
				}
				else if (model is WatcherStatePower watcherStatePower)
				{
					playerChoiceContext.PushModel(model);
					try
					{
						await watcherStatePower.BeforeHandDrawCompat(player, playerChoiceContext);
						model.InvokeExecutionFinished();
					}
					finally
					{
						playerChoiceContext.PopModel(model);
					}
				}
			}
		});
	}
}
[HarmonyPatch]
internal static class WatcherAfterPowerAmountChangedCompatPatch
{
	private static bool Prepare()
	{
		return WatcherHookCompat.HasHookMethod("AfterPowerAmountChanged");
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		return WatcherHookCompat.FindHookMethods("AfterPowerAmountChanged");
	}

	private static void Postfix(ref Task __result, object combatState, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		__result = WatcherHookCompat.ContinueWith(__result, async delegate
		{
			foreach (AbstractModel model in WatcherHookCompat.GetHookListeners(combatState))
			{
				if (model is Mantra mantra)
				{
					await mantra.AfterPowerAmountChangedCompat(power, amount, applier, cardSource);
					model.InvokeExecutionFinished();
				}
			}
		});
	}
}
[HarmonyPatch]
internal static class WatcherAfterSideTurnStartCompatPatch
{
	private static bool Prepare()
	{
		return WatcherHookCompat.HasHookMethod("AfterSideTurnStart");
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		return WatcherHookCompat.FindHookMethods("AfterSideTurnStart");
	}

	private static void Postfix(ref Task __result, object combatState, CombatSide side)
	{
		__result = WatcherHookCompat.ContinueWith(__result, async delegate
		{
			foreach (AbstractModel model in WatcherHookCompat.GetHookListeners(combatState))
			{
				if (!(model is ForecastedMovesPower forecastedMovesPower))
				{
					if (model is DeepThoughtSleepPower deepThoughtSleepPower)
					{
						await deepThoughtSleepPower.AfterSideTurnStartCompat(side, combatState);
						model.InvokeExecutionFinished();
					}
				}
				else
				{
					await forecastedMovesPower.AfterSideTurnStartCompat(side);
					model.InvokeExecutionFinished();
				}
			}
		});
	}
}
internal static class WatcherRetainCompat
{
	internal static Task AfterCardRetained(CardModel card)
	{
		if (!(card is WatcherPerseverance watcherPerseverance))
		{
			if (!(card is WatcherSandsOfTime watcherSandsOfTime))
			{
				if (!(card is WatcherWindmillStrike watcherWindmillStrike))
				{
					if (card != null && card.GetType().Name == "WatcherEvictGuest")
					{
						card.EnergyCost.AddThisCombat(-1, reduceOnly: true);
					}
				}
				else
				{
					watcherWindmillStrike.DynamicVars.Damage.BaseValue += watcherWindmillStrike.DynamicVars["MagicNumber"].BaseValue;
				}
			}
			else
			{
				watcherSandsOfTime.EnergyCost.AddThisCombat(-1, reduceOnly: true);
			}
		}
		else
		{
			watcherPerseverance.DynamicVars.Block.BaseValue += watcherPerseverance.DynamicVars["MagicNumber"].BaseValue;
		}
		return Task.CompletedTask;
	}

	internal static Task AfterFlush(Player player, IReadOnlyCollection<CardModel> retainedCards, object? combatState)
	{
		foreach (CardModel retainedCard in retainedCards)
		{
			if (retainedCard.Owner == player && retainedCard.ShouldRetainThisTurn)
			{
				AfterCardRetained(retainedCard);
			}
		}
		foreach (AbstractModel hookListener in WatcherHookCompat.GetHookListeners(combatState))
		{
			if (!(hookListener is EstablishmentPower establishmentPower) || player != establishmentPower.Owner.Player)
			{
				continue;
			}
			foreach (CardModel retainedCard2 in retainedCards)
			{
				if (retainedCard2.Owner == establishmentPower.Owner.Player && retainedCard2.ShouldRetainThisTurn)
				{
					retainedCard2.EnergyCost.AddThisCombat(-establishmentPower.Amount, reduceOnly: true);
				}
			}
		}
		return Task.CompletedTask;
	}

	internal static Task AfterCardRetained(CardModel card, object? combatState)
	{
		AfterCardRetained(card);
		foreach (AbstractModel hookListener in WatcherHookCompat.GetHookListeners(combatState))
		{
			if (hookListener is EstablishmentPower establishmentPower && card.Owner == establishmentPower.Owner.Player && card.ShouldRetainThisTurn)
			{
				card.EnergyCost.AddThisCombat(-establishmentPower.Amount, reduceOnly: true);
			}
		}
		return Task.CompletedTask;
	}
}
[HarmonyPatch]
internal static class WatcherAfterFlushRetainCompatPatch
{
	private const string HookName = "AfterFlush";

	private static bool Prepare()
	{
		return WatcherHookCompat.HasHookMethod("AfterFlush");
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		return WatcherHookCompat.FindHookMethods("AfterFlush");
	}

	private static void Postfix(ref Task __result, object combatState, Player player, PlayerChoiceContext playerChoiceContext, IReadOnlyCollection<CardModel> flushedCards, IReadOnlyCollection<CardModel> retainedCards)
	{
		__result = WatcherHookCompat.ContinueWith(__result, () => WatcherRetainCompat.AfterFlush(player, retainedCards, combatState));
	}
}
[HarmonyPatch]
internal static class WatcherAfterCardRetainedCompatPatch
{
	private const string HookName = "AfterCardRetained";

	private static bool Prepare()
	{
		return WatcherHookCompat.HasHookMethod("AfterCardRetained");
	}

	private static IEnumerable<MethodBase> TargetMethods()
	{
		return WatcherHookCompat.FindHookMethods("AfterCardRetained");
	}

	private static void Postfix(ref Task __result, object combatState, CardModel card)
	{
		__result = WatcherHookCompat.ContinueWith(__result, () => WatcherRetainCompat.AfterCardRetained(card, combatState));
	}
}
public sealed class WatcherIntentProxy : WatcherCard
{
	private static readonly FieldInfo? _titleLocStringField = typeof(CardModel).GetField("_titleLocString", BindingFlags.Instance | BindingFlags.NonPublic);

	public override bool CanBeGeneratedInCombat => false;

	public override bool CanBeGeneratedByModifiers => false;

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[3]
	{
		new StringVar("EnemyName"),
		new StringVar("IntentTitle"),
		new StringVar("IntentSummary")
	};

	public Creature? AttachedEnemy { get; private set; }

	public MoveState? AttachedMove { get; private set; }

	public WatcherIntentProxy()
		: base(0, CardType.Skill, CardRarity.Token, TargetType.Self, shouldShowInCardLibrary: false)
	{
	}

	public void ConfigureFromEnemy(Creature enemy)
	{
		AttachedEnemy = enemy;
		string enemyName = SafeEnemyName(enemy);
		string intentTitle = SafeIntentTitle(enemy);
		string intentSummary = SafeIntentSummary(enemy);
		PopulateVars(enemyName, intentTitle, intentSummary);
	}

	public void ConfigureFromMove(MoveState move, Creature ownerEnemy, string? slotLabel = null)
	{
		AttachedMove = move;
		AttachedEnemy = ownerEnemy;
		string text = SafeEnemyName(ownerEnemy);
		if (!string.IsNullOrEmpty(slotLabel))
		{
			text = "[" + slotLabel + "] " + text;
		}
		string intentTitle = SafeIntentTitleFromMove(move, ownerEnemy);
		string intentSummary = SafeIntentSummaryFromMove(move, ownerEnemy);
		PopulateVars(text, intentTitle, intentSummary);
	}

	private void PopulateVars(string enemyName, string intentTitle, string intentSummary)
	{
		if (base.DynamicVars["EnemyName"] is StringVar stringVar)
		{
			stringVar.StringValue = enemyName;
		}
		if (base.DynamicVars["IntentTitle"] is StringVar stringVar2)
		{
			stringVar2.StringValue = intentTitle;
		}
		if (base.DynamicVars["IntentSummary"] is StringVar stringVar3)
		{
			stringVar3.StringValue = intentSummary;
		}
		LocString locString = new LocString("cards", base.Id.Entry + ".title");
		locString.Add("EnemyName", enemyName);
		locString.Add("IntentTitle", intentTitle);
		locString.Add("IntentSummary", intentSummary);
		_titleLocStringField?.SetValue(this, locString);
	}

	private static string SafeEnemyName(Creature enemy)
	{
		try
		{
			return enemy?.Name ?? "";
		}
		catch
		{
			return "";
		}
	}

	private static string SafeIntentTitle(Creature enemy)
	{
		try
		{
			if (enemy == null)
			{
				return "";
			}
			AbstractIntent abstractIntent = enemy.Monster?.NextMove?.Intents?.FirstOrDefault();
			if (abstractIntent == null)
			{
				return "";
			}
			return abstractIntent.GetHoverTip(WatcherCreatureCompat.GetCombatState(enemy)?.Creatures.Where((Creature c) => c.IsAlive) ?? System.Array.Empty<Creature>(), enemy).Title ?? "";
		}
		catch (Exception ex)
		{
			Log.Warn("[Watcher] IntentProxy title extraction failed: " + ex.Message);
			return "";
		}
	}

	private static string SafeIntentSummary(Creature enemy)
	{
		try
		{
			if (enemy == null)
			{
				return "";
			}
			IReadOnlyList<AbstractIntent> readOnlyList = enemy.Monster?.NextMove?.Intents ?? System.Array.Empty<AbstractIntent>();
			if (readOnlyList.Count == 0)
			{
				return "";
			}
			IEnumerable<Creature> targets = WatcherCreatureCompat.GetCombatState(enemy)?.Creatures.Where((Creature c) => c.IsAlive) ?? System.Array.Empty<Creature>();
			List<string> list = new List<string>();
			foreach (AbstractIntent item in readOnlyList)
			{
				string text = item.GetIntentLabel(targets, enemy).GetFormattedText() ?? "";
				if (!string.IsNullOrWhiteSpace(text))
				{
					list.Add(text);
				}
			}
			string text2 = string.Join(" / ", list);
			MonsterModel? monster = enemy.Monster;
			if (monster != null && monster.NextMove?.CanTransitionAway == false && !string.IsNullOrWhiteSpace(text2))
			{
				text2 = "[" + text2 + "]";
			}
			return text2;
		}
		catch (Exception ex)
		{
			Log.Warn("[Watcher] IntentProxy summary extraction failed: " + ex.Message);
			return "";
		}
	}

	private static string SafeIntentTitleFromMove(MoveState move, Creature owner)
	{
		try
		{
			AbstractIntent abstractIntent = move?.Intents?.FirstOrDefault();
			if (abstractIntent == null || owner == null)
			{
				return "";
			}
			IEnumerable<Creature> targets = WatcherCreatureCompat.GetCombatState(owner)?.Creatures.Where((Creature c) => c.IsAlive) ?? System.Array.Empty<Creature>();
			return abstractIntent.GetHoverTip(targets, owner).Title ?? "";
		}
		catch (Exception ex)
		{
			Log.Warn("[Watcher] IntentProxy title-from-move failed: " + ex.Message);
			return "";
		}
	}

	private static string SafeIntentSummaryFromMove(MoveState move, Creature owner)
	{
		try
		{
			IReadOnlyList<AbstractIntent> readOnlyList = move?.Intents ?? System.Array.Empty<AbstractIntent>();
			if (readOnlyList.Count == 0 || owner == null)
			{
				return "";
			}
			IEnumerable<Creature> targets = WatcherCreatureCompat.GetCombatState(owner)?.Creatures.Where((Creature c) => c.IsAlive) ?? System.Array.Empty<Creature>();
			List<string> list = new List<string>();
			foreach (AbstractIntent item in readOnlyList)
			{
				string text = item.GetIntentLabel(targets, owner).GetFormattedText() ?? "";
				if (!string.IsNullOrWhiteSpace(text))
				{
					list.Add(text);
				}
			}
			string text2 = string.Join(" / ", list);
			if (!move.CanTransitionAway && !string.IsNullOrWhiteSpace(text2))
			{
				text2 = "[" + text2 + "]";
			}
			return text2;
		}
		catch (Exception ex)
		{
			Log.Warn("[Watcher] IntentProxy summary-from-move failed: " + ex.Message);
			return "";
		}
	}

	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return Task.CompletedTask;
	}
}
internal static class WatcherIntentSelector
{
	private static List<WatcherIntentProxy> CreateProxies(Player owner, IEnumerable<Creature> enemies)
	{
		CombatState combatState = WatcherCreatureCompat.GetCombatState(owner.Creature) ?? throw new InvalidOperationException("WatcherIntentSelector requires an active CombatState.");
		List<WatcherIntentProxy> list = new List<WatcherIntentProxy>();
		foreach (Creature enemy in enemies)
		{
			if (enemy != null && enemy.IsAlive)
			{
				WatcherIntentProxy watcherIntentProxy = combatState.CreateCard<WatcherIntentProxy>(owner);
				watcherIntentProxy.ConfigureFromEnemy(enemy);
				list.Add(watcherIntentProxy);
			}
		}
		return list;
	}

	private static void DisposeProxies(Player owner, IEnumerable<WatcherIntentProxy> proxies)
	{
		CombatState combatState = WatcherCreatureCompat.GetCombatState(owner.Creature);
		if (combatState == null)
		{
			return;
		}
		foreach (WatcherIntentProxy proxy in proxies)
		{
			try
			{
				combatState.RemoveCard(proxy);
			}
			catch
			{
			}
		}
	}

	public static async Task<Creature?> PickOneIntent(PlayerChoiceContext choiceContext, Player owner, IEnumerable<Creature> enemies, LocString prompt, bool cancelable = false)
	{
		List<WatcherIntentProxy> proxies = CreateProxies(owner, enemies);
		if (proxies.Count == 0)
		{
			return null;
		}
		if (proxies.Count == 1)
		{
			Creature? attachedEnemy = proxies[0].AttachedEnemy;
			DisposeProxies(owner, proxies);
			return attachedEnemy;
		}
		try
		{
			CardSelectorPrefs prefs = WatcherCombatHelper.SetCancelable(new CardSelectorPrefs(prompt, 1, 1), cancelable);
			return ((await CardSelectCmd.FromSimpleGrid(choiceContext, proxies.Cast<CardModel>().ToList(), owner, prefs)).FirstOrDefault() as WatcherIntentProxy)?.AttachedEnemy;
		}
		finally
		{
			DisposeProxies(owner, proxies);
		}
	}

	internal static List<WatcherIntentProxy> CreateProxiesFromMoves(Player owner, IReadOnlyList<(MoveState Move, Creature OwnerEnemy, string? Label)> moves)
	{
		CombatState combatState = WatcherCreatureCompat.GetCombatState(owner.Creature) ?? throw new InvalidOperationException("WatcherIntentSelector requires an active CombatState.");
		List<WatcherIntentProxy> list = new List<WatcherIntentProxy>(moves.Count);
		foreach (var move in moves)
		{
			if (move.Move != null && move.OwnerEnemy != null)
			{
				WatcherIntentProxy watcherIntentProxy = combatState.CreateCard<WatcherIntentProxy>(owner);
				watcherIntentProxy.ConfigureFromMove(move.Move, move.OwnerEnemy, move.Label);
				list.Add(watcherIntentProxy);
			}
		}
		return list;
	}

	internal static void DisposeProxiesPublic(Player owner, IEnumerable<WatcherIntentProxy> proxies)
	{
		DisposeProxies(owner, proxies);
	}

	public static async Task<List<WatcherIntentProxy>> PickFromProxies(PlayerChoiceContext choiceContext, Player owner, List<WatcherIntentProxy> proxies, int minSelect, int maxSelect, LocString prompt, bool cancelable = false)
	{
		List<WatcherIntentProxy> result = new List<WatcherIntentProxy>();
		if (proxies.Count == 0)
		{
			return result;
		}
		if (maxSelect <= 0)
		{
			return result;
		}
		int num = Math.Max(0, Math.Min(minSelect, proxies.Count));
		int maxCount = Math.Max(num, Math.Min(maxSelect, proxies.Count));
		CardSelectorPrefs prefs = WatcherCombatHelper.SetCancelable(new CardSelectorPrefs(prompt, num, maxCount), cancelable);
		foreach (CardModel item2 in await CardSelectCmd.FromSimpleGrid(choiceContext, proxies.Cast<CardModel>().ToList(), owner, prefs))
		{
			if (item2 is WatcherIntentProxy item)
			{
				result.Add(item);
			}
		}
		return result;
	}

	public static async Task<List<Creature>> PickOrder(PlayerChoiceContext choiceContext, Player owner, IEnumerable<Creature> enemies, LocString prompt)
	{
		List<WatcherIntentProxy> proxies = CreateProxies(owner, enemies);
		if (proxies.Count == 0)
		{
			return new List<Creature>();
		}
		if (proxies.Count == 1)
		{
			Creature attachedEnemy = proxies[0].AttachedEnemy;
			DisposeProxies(owner, proxies);
			return (attachedEnemy != null) ? new List<Creature> { attachedEnemy } : new List<Creature>();
		}
		try
		{
			int count = proxies.Count;
			CardSelectorPrefs prefs = WatcherCombatHelper.SetCancelable(new CardSelectorPrefs(prompt, count, count), value: false);
			IEnumerable<CardModel> obj = await CardSelectCmd.FromSimpleGrid(choiceContext, proxies.Cast<CardModel>().ToList(), owner, prefs);
			List<Creature> list = new List<Creature>();
			foreach (CardModel item in obj)
			{
				if (item is WatcherIntentProxy { AttachedEnemy: not null } watcherIntentProxy)
				{
					list.Add(watcherIntentProxy.AttachedEnemy);
				}
			}
			return list;
		}
		finally
		{
			DisposeProxies(owner, proxies);
		}
	}
}
internal static class WatcherHistoryEntryCompat
{
	private static readonly FieldInfo? _roundField = typeof(CombatHistoryEntry).GetField("<RoundNumber>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo? _sideField = typeof(CombatHistoryEntry).GetField("<CurrentSide>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

	public static int GetRoundNumber(CombatHistoryEntry e)
	{
		if (!(_roundField != null))
		{
			return 0;
		}
		return (int)_roundField.GetValue(e);
	}

	public static CombatSide GetCurrentSide(CombatHistoryEntry e)
	{
		if (!(_sideField != null))
		{
			return CombatSide.Player;
		}
		return (CombatSide)_sideField.GetValue(e);
	}
}
internal static class WatcherIntentTimeline
{
	public sealed class Snapshot
	{
		public MonsterState CurrentState;

		public bool PerformedFirstMove;

		public MoveState NextMove;

		public SerializableRng RngState;

		public int StateLogCount;
	}

	private static readonly FieldInfo? _currentStateField = typeof(MonsterMoveStateMachine).GetField("_currentState", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo? _performedFirstMoveField = typeof(MonsterMoveStateMachine).GetField("_performedFirstMove", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo? _monsterNextMoveBacking = typeof(MonsterModel).GetField("<NextMove>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

	private static bool _warnedReflectionMissing;

	private static bool ReflectionReady
	{
		get
		{
			if (_currentStateField != null)
			{
				return _performedFirstMoveField != null;
			}
			return false;
		}
	}

	private static void WarnReflectionMissing()
	{
		if (!_warnedReflectionMissing)
		{
			_warnedReflectionMissing = true;
			Log.Warn("[Watcher] Intent timeline reflection unavailable; forecast disabled. Base game field names changed?");
		}
	}

	public static List<MoveState> ForecastFutureMoves(Creature enemy, int count)
	{
		List<MoveState> list = new List<MoveState>(count);
		if (enemy?.Monster == null || enemy.Monster.MoveStateMachine == null)
		{
			return list;
		}
		if (!ReflectionReady)
		{
			WarnReflectionMissing();
			return list;
		}
		CombatState combatState = WatcherCreatureCompat.GetCombatState(enemy);
		if (combatState == null)
		{
			return list;
		}
		MonsterModel monster = enemy.Monster;
		Rng rng = monster.RunRng?.MonsterAi;
		if (rng == null)
		{
			return list;
		}
		Snapshot snap = new Snapshot
		{
			CurrentState = (MonsterState)_currentStateField.GetValue(monster.MoveStateMachine),
			PerformedFirstMove = (bool)_performedFirstMoveField.GetValue(monster.MoveStateMachine),
			NextMove = monster.NextMove,
			RngState = rng.ToSerializable(),
			StateLogCount = monster.MoveStateMachine.StateLog.Count
		};
		try
		{
			list.Add(monster.NextMove);
			_performedFirstMoveField.SetValue(monster.MoveStateMachine, true);
			IReadOnlyList<Creature> playerCreatures = combatState.PlayerCreatures;
			for (int i = 1; i < count; i++)
			{
				try
				{
					MoveState moveState = monster.MoveStateMachine.RollMove(playerCreatures, enemy, rng);
					list.Add(moveState);
					monster.MoveStateMachine.OnMovePerformed(moveState);
				}
				catch (Exception ex)
				{
					Log.Warn($"[Watcher] Forecast stopped at slot {i}: {ex.Message}");
					break;
				}
			}
		}
		finally
		{
			RestoreSnapshot(monster, rng, snap);
		}
		return list;
	}

	private static void RestoreSnapshot(MonsterModel monster, Rng rng, Snapshot snap)
	{
		if (!ReflectionReady)
		{
			return;
		}
		try
		{
			if (monster.MoveStateMachine != null)
			{
				_currentStateField.SetValue(monster.MoveStateMachine, snap.CurrentState);
				_performedFirstMoveField.SetValue(monster.MoveStateMachine, snap.PerformedFirstMove);
				List<MonsterState> stateLog = monster.MoveStateMachine.StateLog;
				while (stateLog.Count > snap.StateLogCount)
				{
					stateLog.RemoveAt(stateLog.Count - 1);
				}
			}
			_monsterNextMoveBacking?.SetValue(monster, snap.NextMove);
			rng.LoadFromSerializable(snap.RngState);
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] Timeline snapshot restore failed: " + ex.Message);
		}
	}

	public static MonsterPerformedMoveEntry? GetLastPerformedMove(Creature enemy)
	{
		try
		{
			return CombatManager.Instance?.History?.Entries.OfType<MonsterPerformedMoveEntry>().LastOrDefault((MonsterPerformedMoveEntry e) => e.Monster == enemy.Monster);
		}
		catch
		{
			return null;
		}
	}

	public static int SumDamageDealtTo(MonsterPerformedMoveEntry entry, Creature receiver)
	{
		try
		{
			int round = WatcherHistoryEntryCompat.GetRoundNumber(entry);
			CombatSide side = WatcherHistoryEntryCompat.GetCurrentSide(entry);
			return (from e in CombatManager.Instance.History.Entries.OfType<DamageReceivedEntry>()
				where e.Dealer == entry.Monster.Creature && e.Receiver == receiver && WatcherHistoryEntryCompat.GetRoundNumber(e) == round && WatcherHistoryEntryCompat.GetCurrentSide(e) == side
				select e).Sum((DamageReceivedEntry e) => e.Result.UnblockedDamage);
		}
		catch
		{
			return 0;
		}
	}

	public static List<(PowerModel Power, decimal Amount)> GetPowersAppliedTo(MonsterPerformedMoveEntry entry, Creature receiver)
	{
		List<(PowerModel, decimal)> list = new List<(PowerModel, decimal)>();
		try
		{
			int round = WatcherHistoryEntryCompat.GetRoundNumber(entry);
			CombatSide side = WatcherHistoryEntryCompat.GetCurrentSide(entry);
			foreach (PowerReceivedEntry item in from e in CombatManager.Instance.History.Entries.OfType<PowerReceivedEntry>()
				where e.Applier == entry.Monster.Creature && e.Actor == receiver && WatcherHistoryEntryCompat.GetRoundNumber(e) == round && WatcherHistoryEntryCompat.GetCurrentSide(e) == side
				select e)
			{
				list.Add((item.Power, item.Amount));
			}
		}
		catch
		{
		}
		return list;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "Public non-async target; prefix only touches public members")]
[HarmonyPatch(typeof(NCreature), "StartDeathAnim")]
internal static class WatcherJudgmentDeath
{
	private const float DissolveSeconds = 1f;

	private static readonly HashSet<Creature> Judged = new HashSet<Creature>();

	internal static void Mark(Creature creature)
	{
		Judged.Add(creature);
	}

	internal static void Unmark(Creature creature)
	{
		Judged.Remove(creature);
	}

	private static bool Prefix(NCreature __instance, bool shouldRemove, ref float __result)
	{
		Creature entity = __instance.Entity;
		if (entity == null || !Judged.Remove(entity))
		{
			return true;
		}
		MonsterModel monster = entity.Monster;
		if (!shouldRemove || monster == null || !monster.ShouldFadeAfterDeath || NonInteractiveMode.IsActive)
		{
			return true;
		}
		try
		{
			Task deathAnimationTask = __instance.DeathAnimationTask;
			if (deathAnimationTask != null && !deathAnimationTask.IsCompleted)
			{
				__result = 0f;
				return false;
			}
			__instance.DisableInteractionForDeath();
			foreach (NIntent item in __instance.IntentContainer.GetChildren().OfType<NIntent>())
			{
				item.SetFrozen(isFrozen: true);
			}
			if (monster.HasDeathSfx)
			{
				SfxCmd.PlayDeath(monster);
			}
			try
			{
				SpineAnimationAccess spineAnimationAccess = __instance.Visuals?.SpineAnimation ?? default(SpineAnimationAccess);
				if (spineAnimationAccess.IsValid)
				{
					spineAnimationAccess.SetTimeScale(0f);
				}
			}
			catch
			{
			}
			__instance.DeathAnimationTask = Dissolve(__instance, __instance.DeathAnimCancelToken.Token);
			TaskHelper.RunSafely(__instance.DeathAnimationTask);
			__result = 1f;
			return false;
		}
		catch (Exception ex)
		{
			Log.Warn("[Watcher] Judgment death prefix fell back to the normal death: " + ex.Message);
			return true;
		}
	}

	private static async Task Dissolve(NCreature nCreature, CancellationToken cancelToken)
	{
		Tween disableUiTween = nCreature.AnimDisableUi();
		nCreature.AnimHideIntent();
		await Cmd.Wait(0.06f, cancelToken, ignoreCombatEnd: true);
		if (cancelToken.IsCancellationRequested || !GodotObject.IsInstanceValid(nCreature))
		{
			return;
		}
		Task fadeVfx = null;
		if (nCreature.Body.IsVisibleInTree())
		{
			NMonsterDeathVfx nMonsterDeathVfx = NMonsterDeathVfx.Create(nCreature, cancelToken);
			Node parent = nCreature.GetParent();
			parent.AddChildSafely(nMonsterDeathVfx);
			if (nMonsterDeathVfx != null)
			{
				parent.MoveChildSafely(nMonsterDeathVfx, nCreature.GetIndex());
			}
			fadeVfx = nMonsterDeathVfx?.PlayVfx();
		}
		if (SaveManager.Instance.PrefsSave.FastMode != FastModeType.Instant)
		{
			bool flag = disableUiTween.IsValid() && disableUiTween.IsRunning();
			if (flag)
			{
				flag = !(await disableUiTween.AwaitFinished(nCreature));
			}
			if (flag)
			{
				return;
			}
			foreach (IDeathDelayer item in nCreature.GetChildrenRecursive<IDeathDelayer>())
			{
				await item.GetDelayTask();
			}
		}
		if (fadeVfx != null)
		{
			await fadeVfx;
		}
		nCreature.QueueFreeSafely();
	}
}
internal static class WatcherKnowFateHud
{
	private static readonly Color FlameTint = new Color("9E68FF");

	private const float FlameSize = 150f;

	private const float FlameScale = 2.2f;

	private const int FlameFps = 10;

	private const int FlameFrameCount = 10;

	private const int FontSize = 32;

	private static readonly Vector2 LabelOffset = new Vector2(-4f, 20f);

	private const string FlamePathFormat = "res://images/atlases/compressed.sprites/card_template/ancient_flame/ancient_card_flame_{0}.tres";

	private static readonly Vector2 FlameOffset = new Vector2(-80f, 5f);

	private static readonly FieldInfo PlayerField = WatcherFieldAccess.Field(typeof(NEnergyCounter), "_player");

	private static Control? _container;

	private static AnimatedSprite2D? _flame;

	private static Label? _shadowLabel;

	private static Label? _label;

	private static NEnergyCounter? _hostCounter;

	private static int _displayedAmount = -1;

	private static float _popScale = 1f;

	private static float _flashTimer;

	private static bool _connectedToProcessFrame;

	private static SceneTree? _connectedTree;

	private static readonly FieldInfo? TextContainerField = TryBuildTextContainerField();

	public static void Attach(NEnergyCounter counter)
	{
		try
		{
			if ((PlayerField.GetValue(counter) as Player)?.Character?.GetType().Name != "WatcherV2")
			{
				return;
			}
			Destroy();
			_hostCounter = counter;
			_container = new Control
			{
				Name = "WatcherKnowFateHud",
				MouseFilter = Control.MouseFilterEnum.Stop,
				Position = FlameOffset,
				Size = new Vector2(150f, 150f),
				PivotOffset = new Vector2(75f, 75f)
			};
			_container.MouseEntered += OnHovered;
			_container.MouseExited += OnUnhovered;
			SpriteFrames spriteFrames = BuildSpriteFrames();
			_flame = new AnimatedSprite2D
			{
				Name = "Flame",
				SpriteFrames = spriteFrames,
				Animation = "default",
				Autoplay = "default",
				SpeedScale = 1f,
				Centered = true,
				Position = new Vector2(75f, 75f),
				Scale = new Vector2(2.2f, 2.2f),
				Modulate = Colors.White
			};
			_container.AddChild(_flame, forceReadableName: false, Node.InternalMode.Disabled);
			_flame.Play("default");
			_shadowLabel = MakeLabel(new Color(0f, 0f, 0f, 0.7f), LabelOffset + Vector2.One * 2f, addOutline: false);
			_container.AddChild(_shadowLabel, forceReadableName: false, Node.InternalMode.Disabled);
			_label = MakeLabel(new Color(1f, 1f, 1f), LabelOffset, addOutline: true);
			_container.AddChild(_label, forceReadableName: false, Node.InternalMode.Disabled);
			counter.AddChild(_container, forceReadableName: false, Node.InternalMode.Disabled);
			SceneTree tree = counter.GetTree();
			if (tree != null)
			{
				if (_connectedToProcessFrame && _connectedTree != null && GodotObject.IsInstanceValid(_connectedTree))
				{
					_connectedTree.ProcessFrame -= OnProcessFrame;
				}
				tree.ProcessFrame += OnProcessFrame;
				_connectedToProcessFrame = true;
				_connectedTree = tree;
			}
			counter.TreeExiting += Destroy;
			_displayedAmount = -1;
			_popScale = 1f;
			_flashTimer = 0f;
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] KnowFateHud.Attach failed: " + ex.Message);
		}
	}

	private static Label MakeLabel(Color color, Vector2 offset, bool addOutline)
	{
		Label label = new Label
		{
			Text = "0",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Position = offset,
			Size = new Vector2(150f, 150f)
		};
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeFontSizeOverride("font_size", 32);
		if (addOutline)
		{
			label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f));
			label.AddThemeConstantOverride("outline_size", 8);
		}
		return label;
	}

	private static SpriteFrames BuildSpriteFrames()
	{
		SpriteFrames spriteFrames = new SpriteFrames();
		if (!spriteFrames.HasAnimation("default"))
		{
			spriteFrames.AddAnimation("default");
		}
		spriteFrames.SetAnimationLoop("default", loop: true);
		spriteFrames.SetAnimationSpeed("default", 10.0);
		for (int num = spriteFrames.GetFrameCount("default") - 1; num >= 0; num--)
		{
			spriteFrames.RemoveFrame("default", num);
		}
		for (int i = 0; i < 10; i++)
		{
			Texture2D texture2D = BakePurpleFlameFrame(WatcherTextureHelper.LoadTexture($"res://images/atlases/compressed.sprites/card_template/ancient_flame/ancient_card_flame_{i}.tres"));
			if (texture2D != null)
			{
				spriteFrames.AddFrame("default", texture2D);
			}
		}
		return spriteFrames;
	}

	private static Texture2D? BakePurpleFlameFrame(Texture2D? src)
	{
		Image image = BakePurpleImage(src, FlameTint);
		if (image == null)
		{
			return src;
		}
		try
		{
			return ImageTexture.CreateFromImage(image);
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] BakePurpleFlameFrame failed: " + ex.Message);
			return src;
		}
	}

	internal static Image? BakePurpleImage(Texture2D? src, Color tint)
	{
		if (src == null)
		{
			return null;
		}
		try
		{
			int num = 0;
			int num2 = 0;
			Image image;
			int num3;
			int num4;
			int num5;
			int num6;
			int num7;
			int num8;
			if (src is AtlasTexture { Atlas: not null } atlasTexture)
			{
				image = atlasTexture.Atlas.GetImage();
				if (image == null)
				{
					return null;
				}
				Rect2 region = atlasTexture.Region;
				num3 = (int)region.Position.X;
				num4 = (int)region.Position.Y;
				num5 = (int)region.Size.X;
				num6 = (int)region.Size.Y;
				Rect2 margin = atlasTexture.Margin;
				num = (int)margin.Position.X;
				num2 = (int)margin.Position.Y;
				num7 = num5 + (int)margin.Size.X;
				num8 = num6 + (int)margin.Size.Y;
			}
			else
			{
				image = src.GetImage();
				if (image == null)
				{
					return null;
				}
				num3 = 0;
				num4 = 0;
				num5 = image.GetWidth();
				num6 = image.GetHeight();
				num7 = num5;
				num8 = num6;
			}
			if (image.IsCompressed())
			{
				image.Decompress();
			}
			if (image.GetFormat() != Image.Format.Rgba8)
			{
				image.Convert(Image.Format.Rgba8);
			}
			int width = image.GetWidth();
			int height = image.GetHeight();
			if (num7 <= 0 || num8 <= 0 || num5 <= 0 || num6 <= 0)
			{
				return null;
			}
			Image image2 = Image.CreateEmpty(num7, num8, useMipmaps: false, Image.Format.Rgba8);
			for (int i = 0; i < num6; i++)
			{
				int num9 = num4 + i;
				if (num9 < 0 || num9 >= height)
				{
					continue;
				}
				int num10 = num2 + i;
				if (num10 < 0 || num10 >= num8)
				{
					continue;
				}
				for (int j = 0; j < num5; j++)
				{
					int num11 = num3 + j;
					if (num11 < 0 || num11 >= width)
					{
						continue;
					}
					int num12 = num + j;
					if (num12 >= 0 && num12 < num7)
					{
						Color pixel = image.GetPixel(num11, num9);
						if (!(pixel.A <= 0f))
						{
							float num13 = pixel.R * 0.299f + pixel.G * 0.587f + pixel.B * 0.114f;
							image2.SetPixel(num12, num10, new Color(num13 * tint.R, num13 * tint.G, num13 * tint.B, pixel.A));
						}
					}
				}
			}
			return image2;
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] BakePurpleImage failed: " + ex.Message);
			return null;
		}
	}

	public static void Destroy()
	{
		if (_connectedToProcessFrame && _connectedTree != null && GodotObject.IsInstanceValid(_connectedTree))
		{
			_connectedTree.ProcessFrame -= OnProcessFrame;
		}
		_connectedToProcessFrame = false;
		_connectedTree = null;
		if (_container != null && GodotObject.IsInstanceValid(_container))
		{
			NHoverTipSet.Remove(_container);
			_container.QueueFree();
		}
		_container = null;
		_flame = null;
		_label = null;
		_shadowLabel = null;
		_hostCounter = null;
	}

	private static FieldInfo? TryBuildTextContainerField()
	{
		try
		{
			return WatcherFieldAccess.Field(typeof(NHoverTipSet), "_textHoverTipContainer");
		}
		catch
		{
			return null;
		}
	}

	private static void OnHovered()
	{
		if (_container == null || !GodotObject.IsInstanceValid(_container))
		{
			return;
		}
		try
		{
			IHoverTip hoverTip = HoverTipFactory.FromPower<KnowFatePower>(null);
			NHoverTipSet nHoverTipSet = NHoverTipSet.CreateAndShow(_container, hoverTip);
			Vector2 vector = NGame.Instance?.GetViewportRect().Size ?? new Vector2(1920f, 1080f);
			float num = 360f;
			float num2 = 200f;
			if (TextContainerField != null)
			{
				VFlowContainer vFlowContainer = (VFlowContainer)TextContainerField.GetValue(nHoverTipSet);
				if (vFlowContainer != null && GodotObject.IsInstanceValid(vFlowContainer))
				{
					if (vFlowContainer.Size.X > 0f)
					{
						num = vFlowContainer.Size.X;
					}
					if (vFlowContainer.Size.Y > 0f)
					{
						num2 = vFlowContainer.Size.Y;
					}
				}
			}
			Vector2 globalPosition = _container.GlobalPosition;
			Vector2 vector2 = _container.Size * _container.Scale;
			Vector2 globalPosition2 = new Vector2(globalPosition.X + vector2.X + 8f, globalPosition.Y - num2 - 8f);
			globalPosition2.X = Mathf.Clamp(globalPosition2.X, 16f, vector.X - num - 16f);
			globalPosition2.Y = Mathf.Clamp(globalPosition2.Y, 16f, vector.Y - num2 - 16f);
			nHoverTipSet.GlobalPosition = globalPosition2;
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] KnowFateHud.OnHovered failed: " + ex.Message);
		}
	}

	private static void OnUnhovered()
	{
		if (_container != null && GodotObject.IsInstanceValid(_container))
		{
			NHoverTipSet.Remove(_container);
		}
	}

	private static void OnProcessFrame()
	{
		if (_container == null || !GodotObject.IsInstanceValid(_container))
		{
			_connectedToProcessFrame = false;
			return;
		}
		if (_hostCounter == null || !GodotObject.IsInstanceValid(_hostCounter))
		{
			_container.Visible = false;
			return;
		}
		RunState runState = RunManager.Instance?.DebugOnlyGetState();
		Player player = ((runState != null) ? LocalContext.GetMe(runState) : null);
		if (player?.Creature == null || player.Character?.GetType().Name != "WatcherV2")
		{
			_container.Visible = false;
			return;
		}
		_container.Visible = true;
		int num = player.Creature.GetPower<KnowFatePower>()?.Amount ?? 0;
		float num2 = (float)_container.GetProcessDeltaTime();
		if (!(num2 <= 0f))
		{
			if (num != _displayedAmount)
			{
				bool num3 = _displayedAmount >= 0 && num > _displayedAmount;
				_displayedAmount = num;
				string text = num.ToString();
				_label.Text = text;
				_shadowLabel.Text = text;
				_popScale = (num3 ? 1.35f : 1.15f);
				_flashTimer = (num3 ? 0.25f : 0.12f);
			}
			float a = 1f;
			_popScale = Mathf.Lerp(_popScale, 1f, num2 * 9f);
			_container.Scale = Vector2.One * _popScale;
			if (_flashTimer > 0f)
			{
				_flashTimer -= num2;
				float num4 = Mathf.Clamp(_flashTimer / 0.25f, 0f, 1f);
				float num5 = 1f + num4 * 0.5f;
				_flame.Modulate = new Color(num5, num5, num5, a);
			}
			else
			{
				_flame.Modulate = new Color(1f, 1f, 1f, a);
			}
		}
	}
}
[HarmonyPatch(typeof(NEnergyCounter), "_Ready")]
[WatcherPatch(SkipOnAndroid = false, Reason = "Public _Ready override on Control; safe DMD target.")]
internal static class WatcherKnowFateHudPatch
{
	private static void Postfix(NEnergyCounter __instance)
	{
		WatcherKnowFateHud.Attach(__instance);
	}
}
internal static class WatcherKnowFateIconStore
{
	private const string FlamePathFormat = "res://images/atlases/compressed.sprites/card_template/ancient_flame/ancient_card_flame_{0}.tres";

	private const int FrameCount = 10;

	private const double FrameDuration = 0.1;

	private static readonly Color Tint = new Color("9E68FF");

	private static readonly Image?[] _frames = new Image[10];

	private static ImageTexture? _liveTexture;

	private static Vector2I _liveTextureSize;

	private static int _currentFrame;

	private static double _frameAccumulator;

	private static double _lastTickTime;

	private static bool _hooked;

	public static Texture2D? Get()
	{
		EnsureInitialized();
		return _liveTexture;
	}

	private static void EnsureInitialized()
	{
		if (_liveTexture != null && GodotObject.IsInstanceValid(_liveTexture))
		{
			return;
		}
		try
		{
			for (int i = 0; i < 10; i++)
			{
				if (_frames[i] == null)
				{
					Texture2D texture2D = WatcherTextureHelper.LoadTexture($"res://images/atlases/compressed.sprites/card_template/ancient_flame/ancient_card_flame_{i}.tres");
					if (texture2D != null)
					{
						_frames[i] = WatcherKnowFateHud.BakePurpleImage(texture2D, Tint);
					}
				}
			}
			NormalizeFrameSizes();
			Image image = null;
			for (int j = 0; j < 10; j++)
			{
				if (_frames[j] != null)
				{
					image = _frames[j];
					break;
				}
			}
			if (image == null)
			{
				return;
			}
			_liveTexture = ImageTexture.CreateFromImage(image);
			_liveTextureSize = image.GetSize();
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] KnowFateIcon init failed: " + ex.Message);
			return;
		}
		if (!_hooked && Engine.GetMainLoop() is SceneTree sceneTree)
		{
			sceneTree.ProcessFrame += Tick;
			_hooked = true;
			_lastTickTime = (double)Time.GetTicksMsec() / 1000.0;
		}
	}

	private static void NormalizeFrameSizes()
	{
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < 10; i++)
		{
			Image image = _frames[i];
			if (image != null)
			{
				num = Math.Max(num, image.GetWidth());
				num2 = Math.Max(num2, image.GetHeight());
			}
		}
		if (num <= 0 || num2 <= 0)
		{
			return;
		}
		for (int j = 0; j < 10; j++)
		{
			Image image2 = _frames[j];
			if (image2 != null && (image2.GetWidth() != num || image2.GetHeight() != num2))
			{
				Image image3 = Image.CreateEmpty(num, num2, useMipmaps: false, Image.Format.Rgba8);
				int x = (num - image2.GetWidth()) / 2;
				int y = num2 - image2.GetHeight();
				image3.BlitRect(image2, new Rect2I(Vector2I.Zero, image2.GetSize()), new Vector2I(x, y));
				_frames[j] = image3;
			}
		}
	}

	private static void Tick()
	{
		if (_liveTexture == null || !GodotObject.IsInstanceValid(_liveTexture))
		{
			return;
		}
		double num = (double)Time.GetTicksMsec() / 1000.0;
		double num2 = num - _lastTickTime;
		_lastTickTime = num;
		if (num2 <= 0.0)
		{
			return;
		}
		if (num2 > 0.25)
		{
			num2 = 0.25;
		}
		_frameAccumulator += num2;
		if (_frameAccumulator < 0.1)
		{
			return;
		}
		int num3 = (int)(_frameAccumulator / 0.1);
		_frameAccumulator -= (double)num3 * 0.1;
		_currentFrame = (_currentFrame + num3) % 10;
		Image image = _frames[_currentFrame];
		if (image == null)
		{
			return;
		}
		try
		{
			Vector2I size = image.GetSize();
			if (_liveTextureSize.X != size.X || _liveTextureSize.Y != size.Y)
			{
				_liveTexture = ImageTexture.CreateFromImage(image);
				_liveTextureSize = size;
			}
			else
			{
				_liveTexture.Update(image);
			}
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] KnowFateIcon Update failed: " + ex.Message);
		}
	}
}
[HarmonyPatch(typeof(PowerModel), "get_Icon")]
[HarmonyPriority(600)]
[WatcherPatch(SkipOnAndroid = false, Reason = "Property getter on public abstract type; safe DMD target.")]
internal static class WatcherKnowFateIconGetterPatch
{
	private static bool Prefix(PowerModel __instance, ref Texture2D __result)
	{
		if (!(__instance is KnowFatePower))
		{
			return true;
		}
		Texture2D texture2D = WatcherKnowFateIconStore.Get();
		if (texture2D == null)
		{
			return true;
		}
		__result = texture2D;
		return false;
	}
}
[HarmonyPatch(typeof(PowerModel), "get_BigIcon")]
[HarmonyPriority(600)]
[WatcherPatch(SkipOnAndroid = false, Reason = "Property getter on public abstract type; safe DMD target.")]
internal static class WatcherKnowFateBigIconGetterPatch
{
	private static bool Prefix(PowerModel __instance, ref Texture2D __result)
	{
		if (!(__instance is KnowFatePower))
		{
			return true;
		}
		Texture2D texture2D = WatcherKnowFateIconStore.Get();
		if (texture2D == null)
		{
			return true;
		}
		__result = texture2D;
		return false;
	}
}
[WatcherPatch(SkipOnAndroid = true, Reason = "public static struct-returning target; static targets are in the ARM64 native-detour SIGSEGV class. Android runs merely degrade legacy Watcher cards to DeprecatedCard, same as no patch.")]
[HarmonyPatch(typeof(ModelId), "Deserialize")]
internal static class WatcherLegacyCardIdMigrationPatch
{
	private static void Postfix(ref ModelId __result)
	{
		if (!(__result.Category != "CARD") && !__result.Entry.StartsWith("WATCHER_", StringComparison.Ordinal) && ModelDb.GetByIdOrNull<CardModel>(__result) == null)
		{
			ModelId modelId = new ModelId("CARD", "WATCHER_" + __result.Entry);
			if (ModelDb.GetByIdOrNull<CardModel>(modelId) is WatcherCard)
			{
				__result = modelId;
			}
		}
	}
}
internal sealed class WatcherMapMarkerAnimator
{
	private static readonly Color[] StanceColors = new Color[4]
	{
		new Color(0.878f, 0.271f, 0.271f),
		new Color(0.357f, 0.612f, 1f),
		new Color(0.961f, 0.784f, 0.29f),
		new Color(0.62f, 0.408f, 1f)
	};

	private const float ColorStepDuration = 1.5f;

	private const float BreathPeriod = 2f;

	private const float OrbAlphaBase = 0.75f;

	private const float OrbAlphaAmplitude = 0.2f;

	private const float OrbScaleMultiplier = 0.55f;

	private const string OrbNodeName = "WatcherMarkerOrb";

	private readonly NMapMarker _marker;

	private readonly TextureRect _orb;

	private float _time;

	private bool _disposed;

	public bool IsAlive
	{
		get
		{
			if (!_disposed && GodotObject.IsInstanceValid(_marker) && GodotObject.IsInstanceValid(_orb))
			{
				return _orb.IsInsideTree();
			}
			return false;
		}
	}

	private WatcherMapMarkerAnimator(NMapMarker marker, TextureRect orb)
	{
		_marker = marker;
		_orb = orb;
	}

	public static void Attach(NMapMarker marker)
	{
		if (marker != null && GodotObject.IsInstanceValid(marker) && !marker.HasNode("WatcherMarkerOrb"))
		{
			Texture2D softHalo = WatcherMapMarkerTextures.SoftHalo;
			TextureRect textureRect = new TextureRect
			{
				Name = "WatcherMarkerOrb",
				Texture = softHalo,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				StretchMode = TextureRect.StretchModeEnum.Scale,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				Material = new CanvasItemMaterial
				{
					BlendMode = CanvasItemMaterial.BlendModeEnum.Add
				}
			};
			marker.AddChild(textureRect, forceReadableName: false, Node.InternalMode.Disabled);
			WatcherMapMarkerAnimator watcherMapMarkerAnimator = new WatcherMapMarkerAnimator(marker, textureRect);
			watcherMapMarkerAnimator.LayoutChildren();
			watcherMapMarkerAnimator.ApplyInitialState();
			textureRect.TreeExiting += watcherMapMarkerAnimator.Dispose;
			WatcherMapMarkerAnimManager.Register(watcherMapMarkerAnimator);
		}
	}

	private void LayoutChildren()
	{
		Vector2 vector = _marker.Size;
		if (vector.X <= 0f || vector.Y <= 0f)
		{
			vector = new Vector2(40f, 40f);
		}
		Vector2 vector2 = vector * 0.55f;
		_orb.Size = vector2;
		_orb.Position = (vector - vector2) * 0.5f;
		_orb.PivotOffset = vector2 * 0.5f;
	}

	private void ApplyInitialState()
	{
		Color modulate = StanceColors[0];
		modulate.A = 0.75f;
		_orb.Modulate = modulate;
		_orb.Scale = Vector2.One;
	}

	public void Tick(float delta)
	{
		if (!IsAlive)
		{
			Dispose();
			return;
		}
		_time += delta;
		if (Math.Abs((_marker.Size * 0.55f).X - _orb.Size.X) > 0.5f)
		{
			LayoutChildren();
		}
		UpdateOrbColor();
		UpdateOrbBreath();
	}

	private void UpdateOrbColor()
	{
		float num = _time / 1.5f;
		int num2 = (int)Math.Floor(num) % StanceColors.Length;
		if (num2 < 0)
		{
			num2 += StanceColors.Length;
		}
		int num3 = (num2 + 1) % StanceColors.Length;
		float num4 = num - (float)Math.Floor(num);
		float weight = ((num4 < 0.5f) ? 0f : Smoothstep(0.5f, 1f, num4));
		Color color = StanceColors[num2];
		Color to = StanceColors[num3];
		Color color2 = color.Lerp(to, weight);
		Color modulate = _orb.Modulate;
		modulate.R = color2.R;
		modulate.G = color2.G;
		modulate.B = color2.B;
		_orb.Modulate = modulate;
	}

	private void UpdateOrbBreath()
	{
		float num = (float)Math.PI;
		float num2 = (float)Math.Sin(_time * num);
		float a = 0.75f + 0.2f * num2;
		Color modulate = _orb.Modulate;
		modulate.A = a;
		_orb.Modulate = modulate;
	}

	private static float Smoothstep(float a, float b, float x)
	{
		float num = Math.Clamp((x - a) / (b - a), 0f, 1f);
		return num * num * (3f - 2f * num);
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			WatcherMapMarkerAnimManager.Unregister(this);
		}
	}
}
internal static class WatcherMapMarkerAnimManager
{
	private static readonly List<WatcherMapMarkerAnimator> _animators = new List<WatcherMapMarkerAnimator>();

	private static bool _hooked;

	private static double _lastTime;

	public static void Register(WatcherMapMarkerAnimator anim)
	{
		EnsureHooked();
		_animators.Add(anim);
	}

	public static void Unregister(WatcherMapMarkerAnimator anim)
	{
		_animators.Remove(anim);
	}

	private static void EnsureHooked()
	{
		if (!_hooked && Engine.GetMainLoop() is SceneTree sceneTree)
		{
			sceneTree.ProcessFrame += Tick;
			_hooked = true;
			_lastTime = (double)Time.GetTicksMsec() / 1000.0;
		}
	}

	private static void Tick()
	{
		double num = (double)Time.GetTicksMsec() / 1000.0;
		float num2 = (float)Math.Max(0.0, num - _lastTime);
		_lastTime = num;
		if (num2 > 0.1f)
		{
			num2 = 0.1f;
		}
		for (int num3 = _animators.Count - 1; num3 >= 0; num3--)
		{
			WatcherMapMarkerAnimator watcherMapMarkerAnimator = _animators[num3];
			if (!watcherMapMarkerAnimator.IsAlive)
			{
				watcherMapMarkerAnimator.Dispose();
				_animators.RemoveAt(num3);
			}
			else
			{
				try
				{
					watcherMapMarkerAnimator.Tick(num2);
				}
				catch (Exception value)
				{
					Log.Error($"[Watcher] marker tick failed: {value}");
				}
			}
		}
	}
}
internal static class WatcherMapMarkerTextures
{
	private const int Size = 96;

	private static ImageTexture? _softHalo;

	public static ImageTexture SoftHalo => _softHalo ?? (_softHalo = BuildSoftHalo());

	private static ImageTexture BuildSoftHalo()
	{
		Image image = Image.CreateEmpty(96, 96, useMipmaps: false, Image.Format.Rgba8);
		float num = 48f;
		float num2 = 48f;
		float num3 = 48f;
		for (int i = 0; i < 96; i++)
		{
			for (int j = 0; j < 96; j++)
			{
				float num4 = (float)j - num;
				float num5 = (float)i - num2;
				float num6 = MathF.Sqrt(num4 * num4 + num5 * num5) / num3;
				if (num6 >= 1f)
				{
					image.SetPixel(j, i, new Color(1f, 1f, 1f, 0f));
					continue;
				}
				float value = MathF.Exp(-5.5f * num6 * num6);
				image.SetPixel(j, i, new Color(1f, 1f, 1f, Math.Clamp(value, 0f, 1f)));
			}
		}
		return ImageTexture.CreateFromImage(image);
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "Public NMapMarker.Initialize Postfix; only adds child Controls, no protected access")]
[HarmonyPatch(typeof(NMapMarker), "Initialize")]
internal static class NMapMarkerWatcherInitPatch
{
	private static void Postfix(NMapMarker __instance, Player player)
	{
		try
		{
			if (player?.Character is Watcher && player.RunState != null && player.RunState.Players.Count == 1)
			{
				WatcherMapMarkerAnimator.Attach(__instance);
			}
		}
		catch (Exception value)
		{
			Log.Error($"[Watcher] marker init patch failed: {value}");
		}
	}
}
public sealed class WatcherStatePower : PowerModel
{
	private CardType? _lastPlayedCardType;

	private int _cardsPlayedThisTurn;

	private int _attacksPlayedThisTurn;

	private int _totalMantraGainedThisCombat;

	private int _mantraGainedThisTurn;

	private int _cataclysmPlaysThisCombat;

	private int _prophecyPlaysThisCombat;

	private int _prophecyPlaysThisTurn;

	private int _knowFateLastObserved;

	private bool _knowFateConsumedThisTurn;

	private bool _knowFateConsumptionAttemptedThisCard;

	private readonly List<CardModel> _deferredRetainCards = new List<CardModel>();

	private readonly HashSet<MonsterPerformedMoveEntry> _undoneMonsterMoves = new HashSet<MonsterPerformedMoveEntry>();

	protected override bool IsVisibleInternal => false;

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.None;

	public CardType? LastPlayedCardType
	{
		get
		{
			return _lastPlayedCardType;
		}
		set
		{
			AssertMutable();
			_lastPlayedCardType = value;
		}
	}

	public int CardsPlayedThisTurn
	{
		get
		{
			return _cardsPlayedThisTurn;
		}
		set
		{
			AssertMutable();
			_cardsPlayedThisTurn = value;
		}
	}

	public int AttacksPlayedThisTurn
	{
		get
		{
			return _attacksPlayedThisTurn;
		}
		set
		{
			AssertMutable();
			_attacksPlayedThisTurn = value;
		}
	}

	public int TotalMantraGainedThisCombat
	{
		get
		{
			return _totalMantraGainedThisCombat;
		}
		set
		{
			AssertMutable();
			_totalMantraGainedThisCombat = value;
		}
	}

	public int MantraGainedThisTurn
	{
		get
		{
			return _mantraGainedThisTurn;
		}
		set
		{
			AssertMutable();
			_mantraGainedThisTurn = value;
		}
	}

	public int CataclysmPlaysThisCombat
	{
		get
		{
			return _cataclysmPlaysThisCombat;
		}
		set
		{
			AssertMutable();
			_cataclysmPlaysThisCombat = value;
		}
	}

	public int ProphecyPlaysThisCombat
	{
		get
		{
			return _prophecyPlaysThisCombat;
		}
		set
		{
			AssertMutable();
			_prophecyPlaysThisCombat = value;
		}
	}

	public int ProphecyPlaysThisTurn
	{
		get
		{
			return _prophecyPlaysThisTurn;
		}
		set
		{
			AssertMutable();
			_prophecyPlaysThisTurn = value;
		}
	}

	public bool KnowFateConsumedThisTurn
	{
		get
		{
			return _knowFateConsumedThisTurn;
		}
		set
		{
			AssertMutable();
			_knowFateConsumedThisTurn = value;
		}
	}

	public bool KnowFateConsumptionAttemptedThisCard
	{
		get
		{
			return _knowFateConsumptionAttemptedThisCard;
		}
		set
		{
			AssertMutable();
			_knowFateConsumptionAttemptedThisCard = value;
		}
	}

	public int KnowFateLastObserved
	{
		get
		{
			return _knowFateLastObserved;
		}
		set
		{
			AssertMutable();
			_knowFateLastObserved = value;
		}
	}

	internal void DeferRetainCard(CardModel card)
	{
		if (card != null)
		{
			_deferredRetainCards.Add(card);
		}
	}

	internal bool TryRegisterUndoneMove(MonsterPerformedMoveEntry entry)
	{
		if (entry == null)
		{
			return false;
		}
		return _undoneMonsterMoves.Add(entry);
	}

	internal bool IsMoveUndone(MonsterPerformedMoveEntry entry)
	{
		if (entry == null)
		{
			return false;
		}
		return _undoneMonsterMoves.Contains(entry);
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (cardPlay.Card.Owner == base.Owner.Player)
		{
			CardsPlayedThisTurn++;
			LastPlayedCardType = cardPlay.Card.Type;
			if (cardPlay.Card.Type == CardType.Attack)
			{
				AttacksPlayedThisTurn++;
			}
			if (cardPlay.Card is IProphecyCard)
			{
				ProphecyPlaysThisCombat++;
				ProphecyPlaysThisTurn++;
			}
			int powerAmount = base.Owner.GetPowerAmount<KnowFatePower>();
			if (powerAmount < KnowFateLastObserved)
			{
				KnowFateConsumedThisTurn = true;
			}
			KnowFateLastObserved = powerAmount;
			await WatcherCombatHelper.ResolveImplicitProphecyFinality(context, cardPlay);
			KnowFateLastObserved = base.Owner.GetPowerAmount<KnowFatePower>();
			KnowFateConsumptionAttemptedThisCard = false;
		}
	}

	public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != base.Owner.Player)
		{
			return Task.CompletedTask;
		}
		CardsPlayedThisTurn = 0;
		AttacksPlayedThisTurn = 0;
		MantraGainedThisTurn = 0;
		ProphecyPlaysThisTurn = 0;
		LastPlayedCardType = null;
		KnowFateConsumedThisTurn = false;
		KnowFateConsumptionAttemptedThisCard = false;
		KnowFateLastObserved = base.Owner.GetPowerAmount<KnowFatePower>();
		return Task.CompletedTask;
	}

	internal async Task BeforeHandDrawCompat(Player player, PlayerChoiceContext choiceContext)
	{
		if (player != base.Owner.Player || _deferredRetainCards.Count <= 0)
		{
			return;
		}
		List<CardModel> list = _deferredRetainCards.ToList();
		_deferredRetainCards.Clear();
		foreach (CardModel item in list)
		{
			await CardPileCmd.Add(item, PileType.Hand);
		}
	}
}
public sealed class WatcherExtraTurnPower : PowerModel
{
	private static readonly HashSet<ulong> ModGrantedExtraTurnPlayers = new HashSet<ulong>();

	protected override bool IsVisibleInternal => false;

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.None;

	internal static void MarkModGrantedExtraTurn(Player player)
	{
		ModGrantedExtraTurnPlayers.Add(player.NetId);
	}

	internal static bool ConsumeModGrantedExtraTurn(Player player)
	{
		return ModGrantedExtraTurnPlayers.Remove(player.NetId);
	}

	public override bool ShouldTakeExtraTurn(Player player)
	{
		return player == base.Owner.Player;
	}

	public override async Task AfterTakingExtraTurn(Player player)
	{
		if (player == base.Owner.Player)
		{
			MarkModGrantedExtraTurn(player);
			await PowerCmd.Remove(this);
		}
	}
}
internal static class WatcherModSettings
{
	public const string OriginalSkeletonDataPath = "res://animations/characters/watcher/watcher_skel_data.tres";

	public const string CommunitySkeletonDataPath = "res://animations/characters/watcher/v2/watcher_skel_data.tres";

	public const string ProphetSkeletonDataPath = "res://animations/characters/watcher/prophet/watcher_skel_data.tres";

	public const string ProphetEyeBoneName = "杖顶";

	public const string BeautifiedAddonModId = "WatcherBeautified";

	public const string OriginalPortraitPath = "res://images/ui/charSelect/watcherPortrait.png";

	public const string OriginalPortraitFallbackPath = "res://images/ui/charSelect/watcherPortrait.jpg";

	public static readonly Vector2 BeautifiedCharSelectSpinePosition = new Vector2(360f, 103f);

	public static readonly Vector2 BeautifiedCharSelectSpineScale = new Vector2(0.95f, 0.95f);

	public static readonly Color BeautifiedCharSelectBgColor = Color.FromHtml("2e1d30");

	public const string Gen2PortraitPath = "res://images/ui/charSelect/watcherPortrait_v2.jpg";

	public const string ProphetCharSelectSkeletonDataPath = "res://animations/character_select/watcher/prophet/characterselect_prophet_skel_data.tres";

	public static readonly Vector2 BeautifiedProphetCharSelectSpinePosition = new Vector2(465f, 205f);

	public static readonly Vector2 BeautifiedProphetCharSelectSpineScale = new Vector2(1.09f, 1.09f);

	public static readonly float BeautifiedProphetSkeletonScale = 0.22f;

	public static readonly Vector2 BeautifiedProphetRigCenterOffset = new Vector2(-400f, -1700f);

	public static readonly float BeautifiedProphetMerchantScale = 0.32f;

	public static readonly float WatcherMerchantScale = 1.5f;

	public const string ProphetRestImagePath = "res://images/characters/watcher/watcher_prophet_rest.png";

	public static readonly float BeautifiedProphetRestScale = 0.82f;

	public static bool UseCommunitySkeleton => IsBeautifiedAddonLoaded();

	public static bool CommunitySkeletonAvailable => ResourceLoader.Exists("res://animations/characters/watcher/v2/watcher_skel_data.tres");

	public static string ActiveSkeletonDataPath
	{
		get
		{
			if (!UseCommunitySkeleton || !CommunitySkeletonAvailable)
			{
				return "res://animations/characters/watcher/watcher_skel_data.tres";
			}
			return "res://animations/characters/watcher/v2/watcher_skel_data.tres";
		}
	}

	private static bool IsBeautifiedAddonLoaded()
	{
		foreach (Mod mod in ModManager.Mods)
		{
			if (mod.state == ModLoadState.Loaded && mod.manifest?.id == "WatcherBeautified")
			{
				return true;
			}
		}
		return false;
	}
}
internal sealed class WatcherOrbAnimator
{
	private sealed class RayPulse
	{
		public TextureRect Rect;

		public float Phase;

		public float Period = 1f;

		public float Rotation;

		public bool UseFineTexture;

		public float HueOffset;
	}

	private const float XOffset = -28f;

	private const float YOffset = -130f;

	private static readonly Vector2[] AnchorPoints = new Vector2[5]
	{
		new Vector2(1462f, 161f),
		new Vector2(1450f, 179f),
		new Vector2(1447f, 158f),
		new Vector2(1476f, 143f),
		new Vector2(1475f, 173f)
	};

	private const float ImageWidth = 1844f;

	private const float ImageHeight = 853f;

	private const float OrbDiameter = 130f;

	private const float InnerSwirlDiameter = 110f;

	private const float PupilDiameter = 16f;

	private const float AmbientDiameter = 640f;

	private const float ShineDiameter = 26f;

	private static readonly float[] HaloRingDiameters = new float[3] { 148f, 168f, 192f };

	private static readonly float[] HaloRingPulseRates = new float[3] { 0.85f, 1.15f, 0.65f };

	private static readonly float[] HaloRingBreathAmplitudes = new float[3] { 0.12f, 0.14f, 0.1f };

	private static readonly float[] HaloRingBaseAlphas = new float[3] { 0.55f, 0.42f, 0.3f };

	private const int RayPulseCount = 4;

	private const float RayPulseBaseDiameter = 460f;

	private const float RayPulseMinScale = 0.3f;

	private const float RayPulseMaxScale = 2.2f;

	private const float RayPulsePeriodMin = 2.4f;

	private const float RayPulsePeriodMax = 3.6f;

	private const float RayPulsePeakAlpha = 0.85f;

	private const string CoverPath = "res://images/ui/charSelect/watcherPortraitCover.png";

	private const float ShineOffsetX = -10f;

	private const float ShineOffsetY = -12f;

	private const float OuterSwirlRate = 0.16f;

	private const float InnerSwirlRate = -0.24f;

	private const float DwellMin = 0.45f;

	private const float DwellMax = 1.3f;

	private const float MoveDuration = 0.18f;

	private readonly Control _root;

	private readonly TextureRect _ambient;

	private readonly TextureRect _outerSwirl;

	private readonly TextureRect _innerSwirl;

	private readonly RayPulse[] _rayPulses = new RayPulse[4];

	private readonly TextureRect[] _haloRings = new TextureRect[3];

	private readonly float[] _haloRingStaticRotations = new float[3];

	private readonly TextureRect _pupil;

	private readonly TextureRect _shine;

	private readonly TextureRect _cover;

	private readonly RandomNumberGenerator _rng = new RandomNumberGenerator();

	private float _time;

	private int _anchorFrom;

	private int _anchorTo;

	private float _phase;

	private bool _moving;

	private float _stateDuration;

	private float _outerRot;

	private float _innerRot;

	private bool _disposed;

	public bool IsAlive
	{
		get
		{
			if (!_disposed && GodotObject.IsInstanceValid(_root))
			{
				return _root.IsInsideTree();
			}
			return false;
		}
	}

	public WatcherOrbAnimator(Control parent)
	{
		_root = new Control
		{
			Name = "WatcherOrbAnim",
			MouseFilter = Control.MouseFilterEnum.Ignore,
			AnchorRight = 1f,
			AnchorBottom = 1f,
			OffsetLeft = 0f,
			OffsetTop = 0f,
			OffsetRight = 0f,
			OffsetBottom = 0f
		};
		parent.AddChild(_root, forceReadableName: false, Node.InternalMode.Disabled);
		_ambient = MakeTextureRect("Ambient", WatcherOrbTextures.Ambient);
		_outerSwirl = MakeTextureRect("OuterSwirl", WatcherOrbTextures.OuterSwirl);
		_innerSwirl = MakeTextureRect("InnerSwirl", WatcherOrbTextures.InnerSwirl);
		_rng.Randomize();
		for (int i = 0; i < 4; i++)
		{
			RayPulse rayPulse = new RayPulse
			{
				Rect = MakeTextureRect($"RayPulse{i}", null),
				Phase = (float)i / 4f * 3f,
				Period = _rng.RandfRange(2.4f, 3.6f),
				Rotation = _rng.RandfRange(0f, (float)Math.PI * 2f),
				UseFineTexture = (i % 2 == 1),
				HueOffset = _rng.RandfRange(-0.015f, 0.015f)
			};
			rayPulse.Rect.Texture = (rayPulse.UseFineTexture ? WatcherOrbTextures.RaysFine : WatcherOrbTextures.RaysCoarse);
			_rayPulses[i] = rayPulse;
		}
		for (int j = 0; j < _haloRings.Length; j++)
		{
			_haloRings[j] = MakeTextureRect($"HaloRing{j}", WatcherOrbTextures.WarmHaloRing(11 + j * 37));
			_haloRingStaticRotations[j] = _rng.RandfRange(0f, (float)Math.PI * 2f);
		}
		_pupil = MakeTextureRect("Pupil", WatcherOrbTextures.Pupil);
		_shine = MakeTextureRect("Shine", WatcherOrbTextures.Shine);
		_cover = MakeCoverRect();
		int num = 0;
		_root.MoveChild(_ambient, num++);
		_root.MoveChild(_cover, num++);
		for (int k = 0; k < 4; k++)
		{
			_root.MoveChild(_rayPulses[k].Rect, num++);
		}
		for (int l = 0; l < _haloRings.Length; l++)
		{
			_root.MoveChild(_haloRings[l], num++);
		}
		_root.MoveChild(_outerSwirl, num++);
		_root.MoveChild(_innerSwirl, num++);
		_root.MoveChild(_pupil, num++);
		_root.MoveChild(_shine, num++);
		_anchorFrom = 0;
		_anchorTo = 0;
		_phase = 0f;
		_moving = false;
		_stateDuration = _rng.RandfRange(0.45f, 1.3f);
		_root.TreeExiting += OnTreeExiting;
		WatcherOrbAnimManager.Register(this);
	}

	private TextureRect MakeTextureRect(string name, Texture2D? texture)
	{
		TextureRect textureRect = new TextureRect
		{
			Name = name,
			Texture = texture,
			MouseFilter = Control.MouseFilterEnum.Ignore,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
		};
		Color modulate = textureRect.Modulate;
		modulate.A = 0f;
		textureRect.Modulate = modulate;
		_root.AddChild(textureRect, forceReadableName: false, Node.InternalMode.Disabled);
		return textureRect;
	}

	private TextureRect MakeCoverRect()
	{
		TextureRect textureRect = new TextureRect
		{
			Name = "Cover",
			Texture = WatcherTextureHelper.LoadTexture("res://images/ui/charSelect/watcherPortraitCover.png"),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			AnchorRight = 1f,
			AnchorBottom = 1f,
			OffsetLeft = 0f,
			OffsetTop = 0f,
			OffsetRight = 0f,
			OffsetBottom = 0f
		};
		_root.AddChild(textureRect, forceReadableName: false, Node.InternalMode.Disabled);
		return textureRect;
	}

	private void OnTreeExiting()
	{
		Dispose();
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			WatcherOrbAnimManager.Unregister(this);
		}
	}

	public void Tick(float delta)
	{
		if (!IsAlive)
		{
			Dispose();
			return;
		}
		_time += delta;
		_phase += delta;
		_outerRot += delta * 0.16f;
		_innerRot += delta * -0.24f;
		if (_phase >= _stateDuration)
		{
			_phase = 0f;
			if (_moving)
			{
				_moving = false;
				_stateDuration = _rng.RandfRange(0.45f, 1.3f);
				_anchorFrom = _anchorTo;
			}
			else
			{
				_moving = true;
				_stateDuration = 0.18f;
				int num = _anchorTo;
				if (AnchorPoints.Length > 1)
				{
					do
					{
						num = _rng.RandiRange(0, AnchorPoints.Length - 1);
					}
					while (num == _anchorTo);
				}
				_anchorFrom = _anchorTo;
				_anchorTo = num;
			}
		}
		Vector2 size = _root.Size;
		if (size.X <= 0f || size.Y <= 0f)
		{
			return;
		}
		float num2 = Math.Max(size.X / 1844f, size.Y / 853f);
		Vector2 vector = new Vector2(1844f * num2, 853f * num2);
		Vector2 vector2 = (size - vector) * 0.5f;
		Vector2 vector3 = AnchorPoints[_anchorFrom];
		Vector2 to = AnchorPoints[_anchorTo];
		float weight = (_moving ? EaseOutCubic(Math.Clamp(_phase / _stateDuration, 0f, 1f)) : 1f);
		Vector2 vector4 = vector3.Lerp(to, weight) * num2 + vector2;
		float num3 = 1f + 0.02f * MathF.Sin(_time * 1.4f);
		PositionRect(_ambient, vector4, 640f * num2 * num3, 0f);
		float num4 = 0.14f + 0.02f * MathF.Sin(_time * 0.45f);
		float saturation = 0.18f + 0.05f * MathF.Sin(_time * 0.6f);
		for (int i = 0; i < 4; i++)
		{
			RayPulse rayPulse = _rayPulses[i];
			rayPulse.Phase += delta;
			if (rayPulse.Phase >= rayPulse.Period)
			{
				rayPulse.Phase -= rayPulse.Period;
				if (rayPulse.Phase > rayPulse.Period)
				{
					rayPulse.Phase = 0f;
				}
				rayPulse.Period = _rng.RandfRange(2.4f, 3.6f);
				rayPulse.Rotation = _rng.RandfRange(0f, (float)Math.PI * 2f);
				rayPulse.UseFineTexture = _rng.Randf() < 0.5f;
				rayPulse.HueOffset = _rng.RandfRange(-0.015f, 0.015f);
				rayPulse.Rect.Texture = (rayPulse.UseFineTexture ? WatcherOrbTextures.RaysFine : WatcherOrbTextures.RaysCoarse);
			}
			float num5 = rayPulse.Phase / rayPulse.Period;
			float num6 = 1f - MathF.Pow(1f - num5, 2f);
			float num7 = 0.3f + 1.9000001f * num6;
			float a = MathF.Pow(MathF.Sin(num5 * (float)Math.PI), 0.85f) * 0.85f;
			PositionRect(rayPulse.Rect, vector4, 460f * num2 * num7, rayPulse.Rotation);
			float num8 = num4 + rayPulse.HueOffset;
			if (num8 < 0f)
			{
				num8 += 1f;
			}
			else if (num8 > 1f)
			{
				num8 -= 1f;
			}
			Color rgb = Color.FromHsv(num8, saturation, 1f);
			ApplyRgb(rayPulse.Rect, rgb);
			SetAlpha(rayPulse.Rect, a);
		}
		for (int j = 0; j < _haloRings.Length; j++)
		{
			float num9 = HaloRingDiameters[j] * num2;
			float num10 = 1f + HaloRingBreathAmplitudes[j] * MathF.Sin(_time * HaloRingPulseRates[j] + (float)j * 0.7f);
			PositionRect(_haloRings[j], vector4, num9 * num10, _haloRingStaticRotations[j]);
			float a2 = HaloRingBaseAlphas[j] * (0.75f + 0.25f * MathF.Sin(_time * HaloRingPulseRates[j] + (float)j * 1.9f));
			SetAlpha(_haloRings[j], a2);
		}
		PositionRect(_outerSwirl, vector4, 130f * num2 * num3, _outerRot);
		PositionRect(_innerSwirl, vector4, 110f * num2 * num3, _innerRot);
		PositionRect(_pupil, vector4, 16f * num2, 0f);
		Vector2 center = vector4 + new Vector2(-10f, -12f) * num2;
		PositionRect(_shine, center, 26f * num2, 0f);
		SetAlpha(_ambient, 0.55f + 0.1f * MathF.Sin(_time * 0.9f));
		SetAlpha(_outerSwirl, 0.85f + 0.1f * MathF.Sin(_time * 0.8f));
		SetAlpha(_innerSwirl, 0.92f + 0.06f * MathF.Sin(_time * 1.3f + 0.5f));
		SetAlpha(_pupil, 1f);
		SetAlpha(_shine, 0.72f + 0.2f * MathF.Sin(_time * 2.1f));
		SetAlpha(_cover, 1f);
	}

	private static void PositionRect(TextureRect? rect, Vector2 center, float diameter, float rotation)
	{
		if (rect != null)
		{
			Vector2 vector2 = (rect.Size = new Vector2(diameter, diameter));
			rect.Position = center - vector2 * 0.5f;
			rect.PivotOffset = vector2 * 0.5f;
			rect.Rotation = rotation;
		}
	}

	private static void SetAlpha(TextureRect rect, float a)
	{
		Color modulate = rect.Modulate;
		modulate.A = Math.Clamp(a, 0f, 1f);
		rect.Modulate = modulate;
	}

	private static void ApplyRgb(TextureRect rect, Color rgb)
	{
		Color modulate = rect.Modulate;
		modulate.R = rgb.R;
		modulate.G = rgb.G;
		modulate.B = rgb.B;
		rect.Modulate = modulate;
	}

	private static float EaseOutCubic(float t)
	{
		return 1f - MathF.Pow(1f - t, 3f);
	}
}
internal static class WatcherOrbTextures
{
	private struct Vector3
	{
		public float X;

		public float Y;

		public float Z;

		public Vector3(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
		}

		public static Vector3 operator *(Vector3 v, float s)
		{
			return new Vector3(v.X * s, v.Y * s, v.Z * s);
		}
	}

	private static Texture2D? _ambient;

	private static Texture2D? _outerSwirl;

	private static Texture2D? _innerSwirl;

	private static Texture2D? _raysCoarse;

	private static Texture2D? _raysFine;

	private static Texture2D? _pupil;

	private static Texture2D? _shine;

	private static readonly System.Collections.Generic.Dictionary<int, Texture2D> _haloRings = new System.Collections.Generic.Dictionary<int, Texture2D>();

	public static Texture2D Ambient => Resolve(ref _ambient, GenerateAmbient);

	public static Texture2D OuterSwirl => Resolve(ref _outerSwirl, () => GenerateSwirl(17, 0.45f, 1f, 0.55f));

	public static Texture2D InnerSwirl => Resolve(ref _innerSwirl, () => GenerateSwirl(91, 0.95f, 0.85f, 0.42f));

	public static Texture2D RaysCoarse => Resolve(ref _raysCoarse, () => GenerateRays(7, 36f, 1f));

	public static Texture2D RaysFine => Resolve(ref _raysFine, () => GenerateRays(23, 80f, 0.6f, fineStreaks: true));

	public static Texture2D Pupil => Resolve(ref _pupil, GeneratePupil);

	public static Texture2D Shine => Resolve(ref _shine, GenerateShine);

	public static Texture2D WarmHaloRing(int seed)
	{
		if (_haloRings.TryGetValue(seed, out Texture2D value) && GodotObject.IsInstanceValid(value))
		{
			return value;
		}
		Texture2D texture2D = GenerateWarmHaloRing(seed);
		_haloRings[seed] = texture2D;
		return texture2D;
	}

	private static Texture2D Resolve(ref Texture2D? slot, Func<Texture2D> factory)
	{
		if (slot == null || !GodotObject.IsInstanceValid(slot))
		{
			try
			{
				slot = factory();
			}
			catch (Exception ex)
			{
				Log.Error("[Watcher] orb texture gen failed: " + ex.Message);
				slot = ImageTexture.CreateFromImage(Image.CreateEmpty(2, 2, useMipmaps: false, Image.Format.Rgba8));
			}
		}
		return slot;
	}

	private static Texture2D GenerateAmbient()
	{
		return BuildTexture(384, delegate(float t, float angle)
		{
			if (t > 1f)
			{
				return (r: 0f, g: 0f, b: 0f, a: 0f);
			}
			float item = MathF.Pow(MathF.Max(0f, 1f - t), 1.6f) * 0.85f;
			float num = Fbm(MathF.Cos(angle * 1.3f) * 1.1f, MathF.Sin(angle * 1.3f) * 1.1f, 47, 3);
			float num2 = Fbm(MathF.Cos(angle * 2.4f + 0.7f) * 1.5f, MathF.Sin(angle * 2.4f + 0.7f) * 1.5f, 113, 3);
			float item2 = 1f - 0.08f * t;
			float item3 = 0.16f - 0.1f * t + 0.2f * num * (1f - t);
			float item4 = 0.12f - 0.06f * t + 0.18f * num2 * (1f - t);
			return (r: item2, g: item3, b: item4, a: item);
		});
	}

	private static Texture2D GenerateWarmHaloRing(int seed)
	{
		Vector3 orange = new Vector3(1f, 0.3f, 0.3f);
		Vector3 orangeAlt = new Vector3(0.95f, 0.42f, 0.18f);
		Vector3 gold = new Vector3(1f, 0.45f, 0.55f);
		Vector3 goldAlt = new Vector3(0.78f, 0.32f, 0.62f);
		Vector3 yellow = new Vector3(1f, 0.55f, 0.75f);
		Vector3 yellowAlt = new Vector3(0.92f, 0.68f, 0.4f);
		Vector3 amber = new Vector3(0.85f, 0.18f, 0.2f);
		Vector3 amberAlt = new Vector3(0.55f, 0.18f, 0.45f);
		return BuildTexture(384, delegate(float t, float angle)
		{
			if (t > 1f)
			{
				return (r: 0f, g: 0f, b: 0f, a: 0f);
			}
			float num = Fbm(MathF.Cos(angle * 1.7f + (float)seed * 0.11f) * 1.6f, MathF.Sin(angle * 1.7f + (float)seed * 0.11f) * 1.6f, seed + 91, 3) - 0.5f;
			float num2 = Fbm(MathF.Cos(angle * 4.3f + (float)seed * 0.21f) * 2.8f, MathF.Sin(angle * 4.3f + (float)seed * 0.21f) * 2.8f, seed + 137, 3) - 0.5f;
			float num3 = num * 0.42f + num2 * 0.18f;
			float num4 = t + num3;
			if (num4 < 0.15f || num4 > 1f)
			{
				return (r: 0f, g: 0f, b: 0f, a: 0f);
			}
			float x = angle + (t - 0.5f) * 2.4f + (float)seed * 0.07f;
			float num5 = Fbm(MathF.Cos(x) * 3.5f + (float)seed * 0.3f, MathF.Sin(x) * 3.5f, seed, 4);
			float num6 = Fbm(angle * 6f + (float)seed * 0.6f, t * 14f + (float)seed, seed + 47, 3);
			float num7 = MathF.Sin(num4 * (10f + (float)seed * 0.05f) + num5 * 5f);
			num7 = (num7 + 1f) * 0.5f;
			num7 = MathF.Pow(num7, 1.1f);
			float num8 = (num4 - 0.15f) / 0.85f;
			num8 = Math.Clamp(num8 + (num5 - 0.5f) * 0.22f, 0f, 1f);
			float num9 = Fbm(MathF.Cos(angle * 1.5f + (float)seed * 0.31f) * 1.6f, MathF.Sin(angle * 1.5f + (float)seed * 0.31f) * 1.6f, seed + 419, 3);
			Vector3 vector = Lerp(amber, amberAlt, num9 * 0.55f);
			Vector3 vector2 = Lerp(orange, orangeAlt, num9 * 0.55f);
			Vector3 vector3 = Lerp(gold, goldAlt, num9 * 0.5f);
			Vector3 vector4 = Lerp(yellow, yellowAlt, num9 * 0.45f);
			Vector3 vector5 = ((num8 < 0.3f) ? Lerp(vector, vector2, num8 / 0.3f) : ((num8 < 0.6f) ? Lerp(vector2, vector3, (num8 - 0.3f) / 0.3f) : ((!(num8 < 0.85f)) ? Lerp(vector4, vector, (num8 - 0.85f) / 0.15f) : Lerp(vector3, vector4, (num8 - 0.6f) / 0.25f))));
			vector5 *= 0.75f + 0.55f * num7;
			float num10 = ((!(num4 >= 0.5f)) ? Smoothstep((num4 - 0.15f) / 0.35f) : 1f);
			float num11 = ((!(num4 <= 0.62f)) ? (1f - Smoothstep((num4 - 0.62f) / 0.38f)) : 1f);
			float value = num10 * num11 * (0.45f + 0.45f * num6) * (0.55f + 0.35f * num7);
			return (r: Math.Clamp(vector5.X, 0f, 1.2f), g: Math.Clamp(vector5.Y, 0f, 1.2f), b: Math.Clamp(vector5.Z, 0f, 1.2f), a: Math.Clamp(value, 0f, 1f));
		});
	}

	private static Texture2D GenerateSwirl(int seed, float hotspotStrength, float brushPower, float edgeSoftness)
	{
		Vector3 outerCol = new Vector3(0.95f, 0.18f, 0.78f);
		Vector3 midCol = new Vector3(0.78f, 0.1f, 0.58f);
		Vector3 hotCol = new Vector3(0.984f, 0.05f, 0.804f);
		Vector3 innerCol = new Vector3(0.32f, 0.04f, 0.28f);
		Vector3 pupilCol = new Vector3(0.1f, 0.02f, 0.1f);
		return BuildTexture(384, delegate(float t, float angle)
		{
			float x = angle + t * 4.5f + (float)seed * 0.01f;
			float x2 = MathF.Cos(x) * 2.4f + (float)seed * 0.3f;
			float y = MathF.Sin(x) * 2.4f + t * 3.8f;
			float num = Fbm(x2, y, seed, 4);
			float x3 = MathF.Cos(angle * 6f + (float)seed * 0.7f) + t * 14f;
			float y2 = MathF.Sin(angle * 6f + (float)seed * 0.7f) + (float)seed * 0.4f;
			float num2 = Fbm(x3, y2, seed + 31, 3);
			Vector3 a = ((t < 0.06f) ? pupilCol : ((t < 0.2f) ? Lerp(pupilCol, hotCol, Smoothstep((t - 0.06f) / 0.14f)) : ((t < 0.4f) ? Lerp(hotCol, midCol, Smoothstep((t - 0.2f) / 0.2f)) : ((!(t < 0.75f)) ? Lerp(outerCol, innerCol, Smoothstep((t - 0.75f) / 0.25f) * 0.5f) : Lerp(midCol, outerCol, Smoothstep((t - 0.4f) / 0.35f))))));
			float t2 = MathF.Pow(MathF.Max(0f, 1f - t * 4f), 2f) * hotspotStrength;
			a = Lerp(a, hotCol, t2);
			float num3 = 0.55f + 0.85f * num2 * brushPower;
			a *= num3;
			float x4 = MathF.Sin(angle * 5f + t * 9f + num * 6f) * 0.5f + 0.5f;
			x4 = MathF.Pow(x4, 1.6f);
			a *= 0.65f + 0.55f * x4;
			float num4 = Fbm(angle * 30f + (float)seed, t * 28f + (float)seed * 0.2f, seed + 7, 2);
			if (num4 > 0.78f)
			{
				float num5 = (num4 - 0.78f) / 0.22f;
				a = Lerp(a, hotCol * 1.2f, num5 * 0.6f);
			}
			float num6 = Fbm(angle * 18f + (float)seed * 0.5f, t * 22f + (float)seed * 0.4f, seed + 191, 2);
			if (num6 > 0.82f && x4 > 0.55f)
			{
				float num7 = (num6 - 0.82f) / 0.18f;
				a = Lerp(b: new Vector3(1f, 0.78f, 0.3f), a: a, t: num7 * 0.35f);
			}
			float num8 = Fbm(angle * 4.5f - (float)seed * 0.3f, t * 3.5f + (float)seed * 0.15f, seed + 277, 3);
			if (num8 > 0.62f && t > 0.2f && t < 0.55f && num3 < 0.85f)
			{
				float num9 = (num8 - 0.62f) / 0.38f;
				a = Lerp(b: new Vector3(0.18f, 0.45f, 0.55f), a: a, t: num9 * 0.18f);
			}
			float num10 = Fbm(MathF.Cos(angle * 3.5f + (float)seed * 0.5f) * 4f, MathF.Sin(angle * 3.5f + (float)seed * 0.5f) * 4f, seed + 53, 3);
			float num11 = 0.55f + 0.18f * num10;
			float num12 = MathF.Min(1f, num11 + edgeSoftness * 0.5f);
			float num13;
			if (t < num11)
			{
				num13 = 1f;
			}
			else if (t > num12)
			{
				num13 = 0f;
			}
			else
			{
				float t3 = (t - num11) / (num12 - num11);
				num13 = 1f - Smoothstep(t3);
			}
			if (t < 0.03f)
			{
				a = pupilCol;
				num13 = MathF.Max(num13, 1f);
			}
			return (r: Math.Clamp(a.X, 0f, 1.2f), g: Math.Clamp(a.Y, 0f, 1.2f), b: Math.Clamp(a.Z, 0f, 1.2f), a: Math.Clamp(num13, 0f, 1f));
		});
	}

	private static Texture2D GenerateRays(int seed, float sharpness, float mainStrength, bool fineStreaks = false)
	{
		Vector3 whiteCore = new Vector3(1f, 0.99f, 0.88f);
		Vector3 orangeCore = new Vector3(1f, 0.85f, 0.2f);
		Vector3 goldAlt = new Vector3(1f, 0.92f, 0.3f);
		Vector3 pinkAlt = new Vector3(1f, 0.78f, 0.42f);
		Vector3 midCol = new Vector3(0.98f, 0.65f, 0.25f);
		Vector3 midPlum = new Vector3(0.85f, 0.5f, 0.3f);
		Vector3 midRose = new Vector3(1f, 0.72f, 0.3f);
		Vector3 edgeCol = new Vector3(0.65f, 0.32f, 0.18f);
		Vector3 edgeDeepPurple = new Vector3(0.55f, 0.25f, 0.25f);
		Vector3 edgeRust = new Vector3(0.72f, 0.38f, 0.15f);
		return BuildTexture(320, delegate(float t, float angle)
		{
			if (t > 1f)
			{
				return (r: 0f, g: 0f, b: 0f, a: 0f);
			}
			float num = MathF.Pow(MathF.Max(0f, MathF.Cos(angle * 2f + (float)seed * 0.05f)), sharpness);
			num += MathF.Pow(MathF.Max(0f, MathF.Cos(angle * 2f + (float)Math.PI / 2f + (float)seed * 0.05f)), sharpness);
			num += MathF.Pow(MathF.Max(0f, MathF.Cos(angle * 2f - (float)Math.PI / 4f + (float)seed * 0.07f)), sharpness) * 0.6f;
			num += MathF.Pow(MathF.Max(0f, MathF.Cos(angle * 2f + (float)Math.PI / 4f + (float)seed * 0.07f)), sharpness) * 0.6f;
			if (fineStreaks)
			{
				float num2 = (float)Math.PI / 8f;
				num = (MathF.Pow(MathF.Max(0f, MathF.Cos(angle * 2f + num2 + (float)seed * 0.13f)), sharpness) + MathF.Pow(MathF.Max(0f, MathF.Cos(angle * 2f + (float)Math.PI / 2f + num2 + (float)seed * 0.13f)), sharpness)) * 0.85f;
			}
			float num3 = Smoothstep((Fbm(MathF.Cos(angle * 1.6f + (float)seed * 0.4f) * 1.8f, MathF.Sin(angle * 1.6f + (float)seed * 0.4f) * 1.8f, seed + 71, 3) - 0.4f) / 0.18f);
			num *= num3;
			num *= mainStrength;
			num *= MathF.Pow(MathF.Max(0f, 1f - t), 0.55f);
			float num4 = MathF.Pow(MathF.Max(0f, 1f - t * 5f), 2.2f);
			float num5 = MathF.Min(1.2f, num + num4);
			if (num5 < 0.01f)
			{
				return (r: 0f, g: 0f, b: 0f, a: 0f);
			}
			float num6 = Fbm(MathF.Cos(angle * 2.1f + (float)seed * 0.17f) * 1.4f, MathF.Sin(angle * 2.1f + (float)seed * 0.17f) * 1.4f, seed + 211, 3);
			float num7 = Fbm(MathF.Cos(angle * 3.7f - (float)seed * 0.21f) * 1.7f, MathF.Sin(angle * 3.7f - (float)seed * 0.21f) * 1.7f, seed + 367, 3);
			Vector3 a = Lerp(orangeCore, goldAlt, num6);
			a = Lerp(a, pinkAlt, num7 * 0.5f);
			Vector3 a2 = Lerp(midCol, midPlum, num6 * 0.55f);
			a2 = Lerp(a2, midRose, num7 * 0.45f);
			Vector3 a3 = Lerp(edgeCol, edgeDeepPurple, num6 * 0.55f);
			a3 = Lerp(a3, edgeRust, num7 * 0.4f);
			Vector3 vector = ((t < 0.1f) ? Lerp(whiteCore, a, MathF.Min(0.6f, num * 0.5f)) : ((!(t < 0.45f)) ? Lerp(a2, a3, Smoothstep((t - 0.45f) / 0.55f)) : Lerp(whiteCore, a2, Smoothstep((t - 0.1f) / 0.35f))));
			float item = MathF.Min(1f, num5);
			return (r: vector.X, g: vector.Y, b: vector.Z, a: item);
		});
	}

	private static Texture2D GeneratePupil()
	{
		return BuildTexture(64, delegate(float t, float _)
		{
			if (t > 1f)
			{
				return (r: 0f, g: 0f, b: 0f, a: 0f);
			}
			float item = ((t < 0.7f) ? 1f : ((!(t > 1f)) ? (1f - Smoothstep((t - 0.7f) / 0.3f)) : 0f));
			return (r: 0.04f, g: 0.02f, b: 0.18f, a: item);
		});
	}

	private static Texture2D GenerateShine()
	{
		return BuildTexture(96, delegate(float t, float _)
		{
			if (t > 1f)
			{
				return (r: 0f, g: 0f, b: 0f, a: 0f);
			}
			float item = MathF.Pow(MathF.Max(0f, 1f - t), 3f);
			return (r: 1f, g: 0.98f, b: 0.9f, a: item);
		});
	}

	private static Texture2D BuildTexture(int size, Func<float, float, (float r, float g, float b, float a)> sample)
	{
		byte[] array = new byte[size * size * 4];
		float num = (float)(size - 1) * 0.5f;
		float num2 = num;
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				float num3 = (float)j - num;
				float num4 = (float)i - num;
				float arg = MathF.Sqrt(num3 * num3 + num4 * num4) / num2;
				float arg2 = MathF.Atan2(num4, num3);
				(float, float, float, float) tuple = sample(arg, arg2);
				float item = tuple.Item1;
				float item2 = tuple.Item2;
				float item3 = tuple.Item3;
				float item4 = tuple.Item4;
				int num5 = (i * size + j) * 4;
				array[num5] = (byte)Math.Clamp((int)(item * 255f + 0.5f), 0, 255);
				array[num5 + 1] = (byte)Math.Clamp((int)(item2 * 255f + 0.5f), 0, 255);
				array[num5 + 2] = (byte)Math.Clamp((int)(item3 * 255f + 0.5f), 0, 255);
				array[num5 + 3] = (byte)Math.Clamp((int)(item4 * 255f + 0.5f), 0, 255);
			}
		}
		return ImageTexture.CreateFromImage(Image.CreateFromData(size, size, useMipmaps: false, Image.Format.Rgba8, array));
	}

	private static Vector3 Lerp(Vector3 a, Vector3 b, float t)
	{
		t = Math.Clamp(t, 0f, 1f);
		return new Vector3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
	}

	private static float Smoothstep(float t)
	{
		t = Math.Clamp(t, 0f, 1f);
		return t * t * (3f - 2f * t);
	}

	private static float Hash(int x, int y, int seed)
	{
		int num = x * 374761393 + y * 668265263 + seed * 1597334677;
		int num2 = (num ^ (num >>> 13)) * 1274126177;
		return (float)(uint)((num2 ^ (num2 >>> 16)) & 0x7FFFFFFF) / 2.1474836E+09f;
	}

	private static float ValueNoise(float x, float y, int seed)
	{
		int num = (int)MathF.Floor(x);
		int num2 = (int)MathF.Floor(y);
		float num3 = x - (float)num;
		float num4 = y - (float)num2;
		float num5 = num3 * num3 * (3f - 2f * num3);
		float num6 = num4 * num4 * (3f - 2f * num4);
		float num7 = Hash(num, num2, seed);
		float num8 = Hash(num + 1, num2, seed);
		float num9 = Hash(num, num2 + 1, seed);
		float num10 = Hash(num + 1, num2 + 1, seed);
		return num7 + (num8 - num7) * num5 + (num9 + (num10 - num9) * num5 - (num7 + (num8 - num7) * num5)) * num6;
	}

	private static float Fbm(float x, float y, int seed, int octaves)
	{
		float num = 0f;
		float num2 = 0.5f;
		float num3 = x;
		float num4 = y;
		for (int i = 0; i < octaves; i++)
		{
			num += num2 * ValueNoise(num3, num4, seed + i * 113);
			num3 *= 2f;
			num4 *= 2f;
			num2 *= 0.5f;
		}
		return num;
	}
}
internal static class WatcherOrbAnimManager
{
	private static readonly List<WatcherOrbAnimator> _animators = new List<WatcherOrbAnimator>();

	private static bool _hooked;

	private static double _lastTime;

	public static void Register(WatcherOrbAnimator animator)
	{
		EnsureHooked();
		_animators.Add(animator);
	}

	public static void Unregister(WatcherOrbAnimator animator)
	{
		_animators.Remove(animator);
	}

	private static void EnsureHooked()
	{
		if (!_hooked && Engine.GetMainLoop() is SceneTree sceneTree)
		{
			sceneTree.ProcessFrame += Tick;
			_hooked = true;
			_lastTime = (double)Time.GetTicksMsec() / 1000.0;
		}
	}

	private static void Tick()
	{
		double num = (double)Time.GetTicksMsec() / 1000.0;
		float num2 = (float)Math.Max(0.0, num - _lastTime);
		_lastTime = num;
		if (num2 > 0.1f)
		{
			num2 = 0.1f;
		}
		for (int num3 = _animators.Count - 1; num3 >= 0; num3--)
		{
			WatcherOrbAnimator watcherOrbAnimator = _animators[num3];
			if (!watcherOrbAnimator.IsAlive)
			{
				watcherOrbAnimator.Dispose();
				_animators.RemoveAt(num3);
			}
			else
			{
				try
				{
					watcherOrbAnimator.Tick(num2);
				}
				catch (Exception value)
				{
					Log.Error($"[Watcher] orb tick failed: {value}");
				}
			}
		}
	}
}
internal static class WatcherTextureHelper
{
	private static readonly System.Collections.Generic.Dictionary<string, Texture2D?> TextureCache = new System.Collections.Generic.Dictionary<string, Texture2D>();

	public static Texture2D? LoadTexture(string path)
	{
		if (TextureCache.TryGetValue(path, out Texture2D value))
		{
			if (value != null && GodotObject.IsInstanceValid(value))
			{
				return value;
			}
			TextureCache.Remove(path);
		}
		Texture2D texture2D = null;
		try
		{
			if (path.StartsWith("res://"))
			{
				if (ResourceLoader.Exists(path))
				{
					texture2D = ResourceLoader.Load<Texture2D>(path, null, ResourceLoader.CacheMode.Reuse);
				}
				if (texture2D == null && Godot.FileAccess.FileExists(path))
				{
					Image image = Image.LoadFromFile(path);
					if (image != null && image.GetWidth() > 0 && image.GetHeight() > 0)
					{
						texture2D = ImageTexture.CreateFromImage(image);
					}
				}
			}
			else
			{
				Image image2 = Image.LoadFromFile(path);
				if (image2.GetWidth() > 0 && image2.GetHeight() > 0)
				{
					texture2D = ImageTexture.CreateFromImage(image2);
				}
			}
		}
		catch
		{
			texture2D = null;
		}
		if (texture2D != null)
		{
			TextureCache[path] = texture2D;
		}
		return texture2D;
	}
}
internal static class WatcherCardRewardSelectionGuard
{
	private static readonly FieldInfo? CompletionSourceField = AccessTools.Field(typeof(NCardRewardSelectionScreen), "_completionSource");

	internal static bool HasPendingSelection(NCardRewardSelectionScreen screen)
	{
		if (CompletionSourceField == null)
		{
			return true;
		}
		object value = CompletionSourceField.GetValue(screen);
		if (value == null)
		{
			return false;
		}
		if (!(value.GetType().GetProperty("Task")?.GetValue(value) is Task task))
		{
			return true;
		}
		return !task.IsCompleted;
	}
}
[HarmonyPatch(typeof(NCardRewardSelectionScreen), "SelectCard")]
internal static class WatcherCardRewardSelectCardGuardPatch
{
	private static bool Prefix(NCardRewardSelectionScreen __instance)
	{
		return WatcherCardRewardSelectionGuard.HasPendingSelection(__instance);
	}
}
[HarmonyPatch(typeof(NCardRewardSelectionScreen), "OnAlternateRewardSelected")]
internal static class WatcherCardRewardAlternateGuardPatch
{
	private static bool Prefix(NCardRewardSelectionScreen __instance)
	{
		return WatcherCardRewardSelectionGuard.HasPendingSelection(__instance);
	}
}
internal static class WatcherCompat
{
	public static bool IsWatcherRun()
	{
		try
		{
			RunState runState = RunManager.Instance?.DebugOnlyGetState();
			if (runState == null)
			{
				return false;
			}
			foreach (Player player in runState.Players)
			{
				if (player?.Character is Watcher)
				{
					return true;
				}
			}
			return false;
		}
		catch
		{
			return false;
		}
	}

	public static bool IsWatcherOwned(RelicModel relic)
	{
		try
		{
			return relic?.Owner?.Character is Watcher;
		}
		catch
		{
			return false;
		}
	}

	public static bool IsExpectedSetupException(Exception ex)
	{
		for (Exception ex2 = ex; ex2 != null; ex2 = ex2.InnerException)
		{
			if (ex2 is NullReferenceException || ex2 is KeyNotFoundException || ex2 is ArgumentException || ex2 is InvalidOperationException || ex2 is IndexOutOfRangeException)
			{
				return true;
			}
		}
		return false;
	}
}
[HarmonyPatch(typeof(AssetCache), "LoadAsset")]
internal static class WatcherAssetCachePatch
{
	private static readonly FieldInfo CacheRef = WatcherFieldAccess.Field(typeof(AssetCache), "_cache");

	private static bool Prefix(AssetCache __instance, string path, ref Resource __result)
	{
		if (!IsWatcherAssetPath(path))
		{
			return true;
		}
		Texture2D texture2D = WatcherTextureHelper.LoadTexture(path);
		if (texture2D != null)
		{
			((ConcurrentDictionary<string, Resource>)CacheRef.GetValue(__instance))[path] = texture2D;
			__result = texture2D;
			return false;
		}
		return true;
	}

	private static bool IsWatcherAssetPath(string path)
	{
		if (!path.Contains("/watcher") && !path.Contains("/Watcher"))
		{
			return path.Contains("char_select_watcher");
		}
		return true;
	}
}
[HarmonyPatch(typeof(ModelDb), "Init")]
internal static class WatcherModelDbInitPatch
{
	private static void Postfix()
	{
		int num = 0;
		Type[] types = Assembly.GetExecutingAssembly().GetTypes();
		foreach (Type type in types)
		{
			if (!type.IsAbstract && !type.IsInterface && typeof(AbstractModel).IsAssignableFrom(type) && !ModelDb.Contains(type))
			{
				try
				{
					ModelDb.Inject(type);
					num++;
				}
				catch (Exception ex)
				{
					Log.Error("[Watcher] Failed to inject missing model " + type.FullName + ": " + ex.Message);
				}
			}
		}
		if (num > 0)
		{
			Log.Info($"[Watcher] Injected {num} model(s) missing from ModelDb.");
		}
	}
}
[HarmonyPatch(typeof(ModelDb), "get_AllCharacters")]
internal static class ModelDbAllCharactersPatch
{
	private static void Postfix(ref IEnumerable<CharacterModel> __result)
	{
		CharacterModel byIdOrNull = ModelDb.GetByIdOrNull<CharacterModel>(ModelDb.GetId(typeof(Watcher)));
		if (byIdOrNull != null)
		{
			__result = __result.Concat(new CharacterModel[1] { byIdOrNull }).Distinct().ToArray();
		}
	}
}
[HarmonyPatch(typeof(EnergyIconHelper), "GetPath", new Type[] { typeof(string) })]
internal static class WatcherEnergyIconPathPatch
{
	private static bool Prefix(string prefix, ref string __result)
	{
		if (prefix == "watcher" || prefix == "prophet")
		{
			__result = "res://images/watcher/card_purple_orb.png";
			return false;
		}
		return true;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "Prefix getter accesses protected CharacterSelectIconPath (Cecil DMD)")]
[HarmonyPatch(typeof(CharacterModel), "get_CharacterSelectIcon")]
internal static class WatcherCharSelectIconPatch
{
	private static bool Prefix(CharacterModel __instance, ref Texture2D __result)
	{
		if (!(__instance is Watcher))
		{
			return true;
		}
		if (ProphetBridge.IsGen2(__instance))
		{
			Texture2D texture2D = WatcherTextureHelper.LoadTexture("res://images/packed/character_select/char_select_prophet.png");
			if (texture2D != null)
			{
				__result = texture2D;
				return false;
			}
		}
		Texture2D texture2D2 = WatcherTextureHelper.LoadTexture("res://images/packed/character_select/char_select_watcher.png");
		if (texture2D2 != null)
		{
			__result = texture2D2;
			return false;
		}
		return true;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "Prefix getter accesses protected CharSelectLockedIconPath (Cecil DMD)")]
[HarmonyPatch(typeof(CharacterModel), "get_CharacterSelectLockedIcon")]
internal static class WatcherCharSelectLockedIconPatch
{
	private static bool Prefix(CharacterModel __instance, ref Texture2D __result)
	{
		if (!(__instance is Watcher))
		{
			return true;
		}
		Texture2D texture2D = WatcherTextureHelper.LoadTexture("res://images/packed/character_select/char_select_watcher_locked.png");
		if (texture2D != null)
		{
			__result = texture2D;
			return false;
		}
		return true;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "Prefix getter accesses protected character icon path (Cecil DMD)")]
[HarmonyPatch(typeof(CharacterModel), "get_IconTexture")]
internal static class WatcherIconTexturePatch
{
	private static bool Prefix(CharacterModel __instance, ref Texture2D __result)
	{
		if (!(__instance is Watcher))
		{
			return true;
		}
		Texture2D texture2D = WatcherTextureHelper.LoadTexture("res://images/ui/top_panel/character_icon_" + __instance.Id.Entry.ToLower() + ".png");
		if (texture2D != null)
		{
			__result = texture2D;
			return false;
		}
		return true;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "Prefix getter accesses protected character outline path (Cecil DMD)")]
[HarmonyPatch(typeof(CharacterModel), "get_IconOutlineTexture")]
internal static class WatcherIconOutlineTexturePatch
{
	private static bool Prefix(CharacterModel __instance, ref Texture2D __result)
	{
		if (!(__instance is Watcher))
		{
			return true;
		}
		Texture2D texture2D = WatcherTextureHelper.LoadTexture("res://images/ui/top_panel/character_icon_" + __instance.Id.Entry.ToLower() + "_outline.png");
		if (texture2D != null)
		{
			__result = texture2D;
			return false;
		}
		return true;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "Prefix getter accesses protected character icon scene (Cecil DMD)")]
[HarmonyPatch(typeof(CharacterModel), "get_Icon")]
internal static class WatcherIconScenePatch
{
	private static bool Prefix(CharacterModel __instance, ref Control __result)
	{
		if (!(__instance is Watcher))
		{
			return true;
		}
		Texture2D texture2D = WatcherTextureHelper.LoadTexture("res://images/ui/top_panel/character_icon_" + __instance.Id.Entry.ToLower() + ".png");
		if (texture2D != null)
		{
			TextureRect textureRect = new TextureRect
			{
				Texture = texture2D,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				AnchorRight = 1f,
				AnchorBottom = 1f,
				GrowHorizontal = Control.GrowDirection.Both,
				GrowVertical = Control.GrowDirection.Both,
				MouseFilter = Control.MouseFilterEnum.Ignore
			};
			__result = textureRect;
			return false;
		}
		return true;
	}
}
internal static class WatcherRelicIconSource
{
	private const string WatcherCookiePath = "res://images/relics/watcher_cookie.png";

	public static string? IconPathFor(RelicModel relic)
	{
		if (relic is WatcherRelic)
		{
			return relic.PackedIconPath;
		}
		if (relic is YummyCookie && !relic.IsCanonical && relic.Owner?.Character is Watcher)
		{
			return "res://images/relics/watcher_cookie.png";
		}
		return null;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "RelicModel Prefix getter calls protected PackedIconPath (Cecil DMD)")]
[HarmonyPatch(typeof(RelicModel), "get_Icon")]
internal static class WatcherRelicIconPatch
{
	private static bool Prefix(RelicModel __instance, ref Texture2D __result)
	{
		string text = WatcherRelicIconSource.IconPathFor(__instance);
		if (text == null)
		{
			return true;
		}
		Texture2D texture2D = WatcherTextureHelper.LoadTexture(text);
		if (texture2D != null)
		{
			__result = texture2D;
			return false;
		}
		return true;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "RelicModel Prefix getter calls protected PackedIconPath (Cecil DMD)")]
[HarmonyPatch(typeof(RelicModel), "get_IconOutline")]
internal static class WatcherRelicIconOutlinePatch
{
	private static bool Prefix(RelicModel __instance, ref Texture2D __result)
	{
		string text = WatcherRelicIconSource.IconPathFor(__instance);
		if (text == null)
		{
			return true;
		}
		Texture2D texture2D = WatcherTextureHelper.LoadTexture(text.Replace("/relics/", "/relics/outline/"));
		if (texture2D != null)
		{
			__result = texture2D;
			return false;
		}
		return true;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "RelicModel Prefix getter (Cecil DMD)")]
[HarmonyPatch(typeof(RelicModel), "get_BigIcon")]
internal static class WatcherRelicBigIconPatch
{
	private static bool Prefix(RelicModel __instance, ref Texture2D __result)
	{
		if (__instance is WatcherRelic)
		{
			return true;
		}
		string text = WatcherRelicIconSource.IconPathFor(__instance);
		if (text == null)
		{
			return true;
		}
		Texture2D texture2D = WatcherTextureHelper.LoadTexture(text);
		if (texture2D != null)
		{
			__result = texture2D;
			return false;
		}
		return true;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "PowerModel Prefix getter calls protected PackedIconPath (Cecil DMD)")]
[HarmonyPatch(typeof(PowerModel), "get_Icon")]
internal static class WatcherPowerIconPatch
{
	private static readonly Assembly WatcherAssembly = typeof(Watcher).Assembly;

	private static bool Prefix(PowerModel __instance, ref Texture2D __result)
	{
		if (__instance.GetType().Assembly != WatcherAssembly)
		{
			return true;
		}
		string text = ((__instance is Foreseen) ? ModelDb.Power<KnowFatePower>().Id.Entry.ToLowerInvariant() : __instance.Id.Entry.ToLowerInvariant());
		Texture2D texture2D = WatcherTextureHelper.LoadTexture("res://images/powers/" + text + ".png");
		if (texture2D != null)
		{
			__result = texture2D;
			return false;
		}
		return true;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "PowerModel Prefix getter calls protected PackedIconPath (Cecil DMD)")]
[HarmonyPatch(typeof(PowerModel), "get_BigIcon")]
internal static class WatcherPowerBigIconPatch
{
	private static readonly Assembly WatcherAssembly = typeof(Watcher).Assembly;

	private static bool Prefix(PowerModel __instance, ref Texture2D __result)
	{
		if (__instance.GetType().Assembly != WatcherAssembly)
		{
			return true;
		}
		string text = ((__instance is Foreseen) ? ModelDb.Power<KnowFatePower>().Id.Entry.ToLowerInvariant() : __instance.Id.Entry.ToLowerInvariant());
		Texture2D texture2D = WatcherTextureHelper.LoadTexture("res://images/powers/" + text + ".png");
		if (texture2D != null)
		{
			__result = texture2D;
			return false;
		}
		return true;
	}
}
[HarmonyPatch(typeof(CharacterModel), "get_AttackSfx")]
internal static class WatcherAttackSfxPatch
{
	private static void Postfix(CharacterModel __instance, ref string __result)
	{
		if (__instance.GetType().Name == "WatcherV2")
		{
			WatcherV2AssetRedirect.Apply(ref __result);
		}
		else if (__instance is Watcher)
		{
			__result = "event:/sfx/characters/necrobinder/necrobinder_attack";
		}
	}
}
[HarmonyPatch(typeof(CharacterModel), "get_CastSfx")]
internal static class WatcherCastSfxPatch
{
	private static void Postfix(CharacterModel __instance, ref string __result)
	{
		if (__instance.GetType().Name == "WatcherV2")
		{
			WatcherV2AssetRedirect.Apply(ref __result);
		}
		else if (__instance is Watcher)
		{
			__result = "event:/sfx/characters/necrobinder/necrobinder_cast";
		}
	}
}
[HarmonyPatch(typeof(CharacterModel), "get_DeathSfx")]
internal static class WatcherDeathSfxPatch
{
	private static void Postfix(CharacterModel __instance, ref string __result)
	{
		if (__instance.GetType().Name == "WatcherV2")
		{
			WatcherV2AssetRedirect.Apply(ref __result);
		}
		else if (__instance is Watcher)
		{
			__result = "event:/sfx/characters/necrobinder/necrobinder_die";
		}
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "CardModel Prefix getter calls protected PortraitPath (Cecil DMD)")]
[HarmonyPatch(typeof(CardModel), "get_Portrait")]
internal static class WatcherCardModelPortraitPatch
{
	private static bool Prefix(CardModel __instance, ref Texture2D __result)
	{
		if (!(__instance.Pool is WatcherCardPool))
		{
			return true;
		}
		Texture2D texture2D = WatcherTextureHelper.LoadTexture(WatcherCardArtSettings.GetEffectivePortraitPath(__instance));
		if (texture2D != null)
		{
			__result = texture2D;
			return false;
		}
		return true;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "CardModel Prefix getter calls protected PortraitPath (Cecil DMD)")]
[HarmonyPatch(typeof(CardModel), "get_HasPortrait")]
internal static class WatcherCardHasPortraitPatch
{
	private static bool Prefix(CardModel __instance, ref bool __result)
	{
		if (!(__instance.Pool is WatcherCardPool))
		{
			return true;
		}
		__result = WatcherTextureHelper.LoadTexture(__instance.PortraitPath) != null || WatcherTextureHelper.LoadTexture("res://images/packed/card_portraits/watcher/_placeholder.png") != null;
		return false;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "CardModel Prefix getter calls protected PortraitPath (Cecil DMD)")]
[HarmonyPatch(typeof(CardModel), "get_HasBetaPortrait")]
internal static class WatcherCardHasBetaPortraitPatch
{
	private static bool Prefix(CardModel __instance, ref bool __result)
	{
		if (!(__instance.Pool is WatcherCardPool))
		{
			return true;
		}
		__result = WatcherTextureHelper.LoadTexture(__instance.BetaPortraitPath) != null;
		return false;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "CardModel overlay getter returns a scene path only")]
[HarmonyPatch(typeof(CardModel), "get_OverlayPath")]
internal static class WatcherCardOverlayPathPatch
{
	private static bool Prefix(CardModel __instance, ref string __result)
	{
		if (!WatcherCard.ShouldUseProphecyGoldOverlay(__instance))
		{
			return true;
		}
		__result = "res://scenes/cards/overlays/watcher_prophecy_gold.tscn";
		return false;
	}
}
[HarmonyPatch(typeof(NCard), "Reload")]
internal static class WatcherCardPortraitPatch
{
	private static readonly FieldInfo PortraitRef = WatcherFieldAccess.Field(typeof(NCard), "_portrait");

	private static void Postfix(NCard __instance)
	{
		CardModel model = __instance.Model;
		if (model?.Pool is WatcherCardPool)
		{
			Texture2D texture2D = WatcherTextureHelper.LoadTexture(WatcherCardArtSettings.GetEffectivePortraitPath(model));
			TextureRect textureRect = (TextureRect)PortraitRef.GetValue(__instance);
			if (texture2D != null && textureRect != null)
			{
				textureRect.Texture = texture2D;
			}
		}
	}
}
[HarmonyPatch(typeof(NCreatureVisuals), "_Ready")]
internal static class WatcherCreatureVisualsPatch
{
	private static void Postfix(NCreatureVisuals __instance)
	{
		if (!(__instance.Name != (StringName)"Watcher") && __instance.GetNodeOrNull<Node2D>("%Visuals") is Sprite2D sprite2D)
		{
			Texture2D texture2D = WatcherTextureHelper.LoadTexture("res://images/characters/watcher/watcher_idle.png");
			if (texture2D != null)
			{
				sprite2D.Texture = texture2D;
			}
		}
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "_Ready Prefix creates MegaSprite from private context (Cecil DMD)")]
[HarmonyPatch(typeof(NMerchantCharacter), "_Ready")]
internal static class WatcherMerchantCharacterPatch
{
	private static bool IsWatcherMerchantScene(NMerchantCharacter instance)
	{
		if (instance.Name == (StringName)"WatcherMerchant")
		{
			return true;
		}
		string sceneFilePath = instance.SceneFilePath;
		if (!string.IsNullOrEmpty(sceneFilePath))
		{
			return sceneFilePath.IndexOf("watcher_merchant", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return false;
	}

	private static bool Prefix(NMerchantCharacter __instance)
	{
		if (!IsWatcherMerchantScene(__instance))
		{
			return true;
		}
		Node node = ((__instance.GetChildCount() > 0) ? __instance.GetChild(0) : null);
		if (!(node is Sprite2D sprite2D))
		{
			if (node == null || node.GetClass() != "SpineSprite")
			{
				return true;
			}
			MegaSprite megaSprite = new MegaSprite(node);
			if (megaSprite.HasAnimation("relaxed_loop"))
			{
				return true;
			}
			if (node is Node2D node2D)
			{
				WatcherSkeletonHelper.ApplySkeletonVariant(megaSprite);
				WatcherSpineCompat.SetAnimation(megaSprite.GetAnimationState(), "Idle");
				if (WatcherModSettings.UseCommunitySkeleton)
				{
					WatcherSkeletonHelper.HideEye(node2D);
				}
				else
				{
					WatcherSkeletonHelper.UpdateEyeTop(node2D, "None");
				}
				node2D.Scale = Vector2.One * WatcherModSettings.WatcherMerchantScale;
				node2D.SetDeferred("scale", Vector2.One * WatcherModSettings.WatcherMerchantScale);
			}
			return false;
		}
		Texture2D texture2D = WatcherTextureHelper.LoadTexture("res://images/characters/watcher/watcher_idle.png");
		if (texture2D != null)
		{
			sprite2D.Texture = texture2D;
		}
		return false;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "Prefix only sets registered Godot properties + connects signals via name")]
[HarmonyPatch(typeof(NRestSiteCharacter), "_Ready")]
internal static class WatcherRestSiteCharacterPatch
{
	private const string WatcherRestSpriteName = "WatcherRestPose";

	private static bool Prefix(NRestSiteCharacter __instance)
	{
		if (!(__instance.Player?.Character is Watcher))
		{
			return true;
		}
		bool flag = false;
		foreach (Node child in __instance.GetChildren())
		{
			if (!(child.GetClass() != "SpineSprite"))
			{
				MegaSprite megaSprite = new MegaSprite(child);
				if (!megaSprite.HasAnimation("overgrowth_loop") && !megaSprite.HasAnimation("hive_loop") && !megaSprite.HasAnimation("glory_loop"))
				{
					flag = true;
					break;
				}
			}
		}
		if (!flag)
		{
			return true;
		}
		Control nodeOrNull = __instance.GetNodeOrNull<Control>("ControlRoot");
		Control nodeOrNull2 = __instance.GetNodeOrNull<Control>("%Hitbox");
		NSelectionReticle nodeOrNull3 = __instance.GetNodeOrNull<NSelectionReticle>("%SelectionReticle");
		Control nodeOrNull4 = __instance.GetNodeOrNull<Control>("%ThoughtBubbleLeft");
		Control nodeOrNull5 = __instance.GetNodeOrNull<Control>("%ThoughtBubbleRight");
		if (nodeOrNull != null)
		{
			__instance.Set(NRestSiteCharacter.PropertyName._controlRoot, nodeOrNull);
		}
		if (nodeOrNull2 != null)
		{
			__instance.Set(NRestSiteCharacter.PropertyName.Hitbox, nodeOrNull2);
		}
		if (nodeOrNull3 != null)
		{
			__instance.Set(NRestSiteCharacter.PropertyName._selectionReticle, nodeOrNull3);
		}
		if (nodeOrNull4 != null)
		{
			__instance.Set(NRestSiteCharacter.PropertyName._leftThoughtAnchor, nodeOrNull4);
		}
		if (nodeOrNull5 != null)
		{
			__instance.Set(NRestSiteCharacter.PropertyName._rightThoughtAnchor, nodeOrNull5);
		}
		if (nodeOrNull2 != null)
		{
			Callable callable = Callable.From(() => __instance.Call(NRestSiteCharacter.MethodName.OnFocus));
			Callable callable2 = Callable.From(() => __instance.Call(NRestSiteCharacter.MethodName.OnUnfocus));
			nodeOrNull2.Connect(Control.SignalName.FocusEntered, callable);
			nodeOrNull2.Connect(Control.SignalName.FocusExited, callable2);
			nodeOrNull2.Connect(Control.SignalName.MouseEntered, callable);
			nodeOrNull2.Connect(Control.SignalName.MouseExited, callable2);
		}
		return false;
	}

	private static void Postfix(NRestSiteCharacter __instance)
	{
		if (!(__instance.Player?.Character is Watcher))
		{
			return;
		}
		foreach (Node child in __instance.GetChildren())
		{
			if (child is Sprite2D { Texture: null } sprite2D)
			{
				Texture2D texture2D = WatcherTextureHelper.LoadTexture("res://images/characters/watcher/watcher_idle.png");
				if (texture2D != null)
				{
					sprite2D.Texture = texture2D;
				}
			}
			else
			{
				if (!(child.GetClass() == "SpineSprite") || !(child is Node2D node2D))
				{
					continue;
				}
				MegaSprite megaSprite = new MegaSprite(child);
				if (!megaSprite.HasAnimation("overgrowth_loop") && !megaSprite.HasAnimation("hive_loop") && !megaSprite.HasAnimation("glory_loop"))
				{
					bool num = WatcherModSettings.UseCommunitySkeleton && ProphetBridge.IsGen2(__instance.Player.Character);
					float num2 = 0.8f;
					Texture2D texture2D2 = null;
					if (num)
					{
						texture2D2 = WatcherTextureHelper.LoadTexture("res://images/characters/watcher/watcher_prophet_rest.png");
						if (texture2D2 != null)
						{
							num2 = WatcherModSettings.BeautifiedProphetRestScale;
						}
					}
					if (texture2D2 == null)
					{
						texture2D2 = WatcherTextureHelper.LoadTexture("res://images/characters/watcher/watcher_rest.png");
					}
					if (texture2D2 != null)
					{
						node2D.Visible = false;
						if (__instance.GetNodeOrNull<Sprite2D>("WatcherRestPose") == null)
						{
							Shader shader = new Shader
							{
								Code = "shader_type canvas_item;\r\nuniform vec3 glow_color : source_color = vec3(0.455, 0.992, 0.992);\r\nuniform float brightness : hint_range(0.0, 1.0) = 0.55;\r\nuniform float diffuse_strength : hint_range(0.0, 2.0) = 0.9;\r\nuniform float specular_strength : hint_range(0.0, 1.5) = 0.35;\r\nuniform float spec_threshold : hint_range(0.3, 1.0) = 0.7;\r\nuniform float flicker_speed = 2.5;\r\nuniform vec2 light_origin = vec2(1.0, 1.0); // bottom-right in UV space\r\nuniform float falloff : hint_range(0.1, 2.0) = 1.0;\r\nuniform float lum_power : hint_range(0.5, 4.0) = 1.6;\r\nvoid fragment() {\r\n    vec4 tex = texture(TEXTURE, UV);\r\n    vec3 base = tex.rgb;\r\n    vec3 dimmed = base * brightness;\r\n\r\n    // Reflectance mask: bright surfaces receive more light.\r\n    float lum_src = dot(base, vec3(0.2126, 0.7152, 0.0722));\r\n    float reflect_mask = pow(clamp(lum_src, 0.0, 1.0), lum_power);\r\n\r\n    float flicker = 0.9 + 0.07 * sin(TIME * flicker_speed)\r\n                        + 0.03 * sin(TIME * flicker_speed * 3.7);\r\n    float dist = distance(UV, light_origin);\r\n    float radial = 1.0 - smoothstep(0.0, falloff, dist);\r\n    float intensity = flicker * radial;\r\n\r\n    // Diffuse: light modulated by surface color —a red shirt stays red-ish under\r\n    // cyan light rather than being recolored outright, which is what makes the\r\n    // highlight read as \"light hitting the body\" instead of a tint overlay.\r\n    vec3 diffuse = base * glow_color * diffuse_strength * intensity * reflect_mask;\r\n\r\n    // Specular: tight rim highlight on the brightest spots only.\r\n    float spec_mask = smoothstep(spec_threshold, 1.0, lum_src);\r\n    vec3 specular = glow_color * specular_strength * intensity * spec_mask;\r\n\r\n    tex.rgb = dimmed + diffuse + specular;\r\n    COLOR = tex;\r\n}"
							};
							ShaderMaterial shaderMaterial = new ShaderMaterial
							{
								Shader = shader
							};
							Color color = TryFindFireColor(__instance) ?? new Color(0.455f, 0.992f, 0.992f);
							shaderMaterial.SetShaderParameter("glow_color", color);
							Sprite2D node = new Sprite2D
							{
								Name = "WatcherRestPose",
								Texture = texture2D2,
								Position = node2D.Position + new Vector2(0f, -110f),
								Scale = new Vector2(num2, num2),
								Material = shaderMaterial
							};
							__instance.AddChild(node, forceReadableName: false, Node.InternalMode.Disabled);
						}
						continue;
					}
					WatcherSkeletonHelper.ApplySkeletonVariant(megaSprite);
					WatcherSpineCompat.SetAnimation(megaSprite.GetAnimationState(), "Idle");
				}
				node2D.Scale = new Vector2(1.5f, 1.5f);
				node2D.SetDeferred("scale", new Vector2(1.5f, 1.5f));
			}
		}
	}

	private static Color? TryFindFireColor(Node start)
	{
		Node node = start;
		while (node.GetParent() != null)
		{
			node = node.GetParent();
		}
		return SearchFireColor(node);
	}

	private static Color? SearchFireColor(Node node)
	{
		try
		{
			if (node is CanvasItem { Material: ShaderMaterial material })
			{
				Variant shaderParameter = material.GetShaderParameter("OuterColor");
				if (shaderParameter.VariantType == Variant.Type.Color)
				{
					return shaderParameter.AsColor();
				}
			}
		}
		catch
		{
		}
		foreach (Node child in node.GetChildren())
		{
			Color? result = SearchFireColor(child);
			if (result.HasValue)
			{
				return result;
			}
		}
		return null;
	}
}
[HarmonyPatch(typeof(SaveManager), "UpdateProgressAfterCombatWon")]
internal static class WatcherCombatWonEpochPatch
{
	private static bool Prefix(Player localPlayer, CombatRoom combatRoom)
	{
		if (!(localPlayer?.Character is Watcher))
		{
			return true;
		}
		ModelId modelId = combatRoom.CombatState?.Encounter?.Id;
		if (modelId != null)
		{
			SaveManager.Instance.Progress.GetOrCreateEncounterStats(modelId);
			SaveManager.Instance.SaveProgressFile();
		}
		return false;
	}
}
internal static class WatcherCharacterSelectBgHelper
{
	public static bool ApplyToContainer(Control? bgContainer, CharacterModel characterModel)
	{
		if (bgContainer == null || !(characterModel is Watcher))
		{
			return false;
		}
		return ApplyToBackground(bgContainer.GetNodeOrNull(characterModel.Id.Entry + "_bg"), ProphetBridge.IsGen2(characterModel));
	}

	public static bool ApplyToExistingWatcherBackgrounds(Control? bgContainer)
	{
		if (bgContainer == null)
		{
			return false;
		}
		bool flag = false;
		string text = ModelDb.GetId(typeof(Watcher)).Entry + "_bg";
		CharacterModel characterModel = ProphetBridge.Gen2Character();
		string text2 = ((characterModel != null) ? (characterModel.Id.Entry + "_bg") : null);
		foreach (Node child in bgContainer.GetChildren())
		{
			string text3 = child.Name.ToString();
			if (text3 == text)
			{
				flag |= ApplyToBackground(child, isGen2: false);
			}
			else if (text3 == text2)
			{
				flag |= ApplyToBackground(child, isGen2: true);
			}
		}
		return flag;
	}

	private static bool ApplyToBackground(Node? watcherBg, bool isGen2)
	{
		if (watcherBg == null)
		{
			return false;
		}
		TextureRect nodeOrNull = watcherBg.GetNodeOrNull<TextureRect>("Portrait");
		Node nodeOrNull2 = watcherBg.GetNodeOrNull("WatcherSpine");
		if (isGen2)
		{
			if (WatcherModSettings.UseCommunitySkeleton && nodeOrNull2 is Node2D node2D && WatcherSkeletonHelper.ApplyCharSelectSkeleton(node2D, "res://animations/character_select/watcher/prophet/characterselect_prophet_skel_data.tres", "animation"))
			{
				node2D.Visible = true;
				node2D.Position = WatcherModSettings.BeautifiedProphetCharSelectSpinePosition;
				node2D.Scale = WatcherModSettings.BeautifiedProphetCharSelectSpineScale;
				WatcherCharSelectSpineFitter.Attach(node2D);
				ColorRect nodeOrNull3 = watcherBg.GetNodeOrNull<ColorRect>("BgColor");
				if (nodeOrNull3 != null)
				{
					nodeOrNull3.Color = WatcherModSettings.BeautifiedCharSelectBgColor;
				}
				if (nodeOrNull != null)
				{
					nodeOrNull.Visible = false;
					RemoveOrbAnimator(nodeOrNull);
				}
				Log.Info("[Watcher] char-select bg applied (gen2 beautified animated portrait)");
				return true;
			}
			if (nodeOrNull2 is CanvasItem canvasItem)
			{
				canvasItem.Visible = false;
			}
			if (nodeOrNull == null)
			{
				return false;
			}
			Texture2D texture2D = WatcherTextureHelper.LoadTexture("res://images/ui/charSelect/watcherPortrait_v2.jpg");
			if (texture2D == null)
			{
				Log.Warn("[Watcher] Gen2 portrait load failed: res://images/ui/charSelect/watcherPortrait_v2.jpg");
				return false;
			}
			Log.Info("[Watcher] portrait → res://images/ui/charSelect/watcherPortrait_v2.jpg (gen2)");
			nodeOrNull.Visible = true;
			nodeOrNull.Texture = texture2D;
			RemoveOrbAnimator(nodeOrNull);
			return true;
		}
		if (WatcherModSettings.UseCommunitySkeleton)
		{
			if (nodeOrNull2 is Node2D node2D2)
			{
				node2D2.Visible = true;
				node2D2.Position = WatcherModSettings.BeautifiedCharSelectSpinePosition;
				node2D2.Scale = WatcherModSettings.BeautifiedCharSelectSpineScale;
			}
			ColorRect nodeOrNull4 = watcherBg.GetNodeOrNull<ColorRect>("BgColor");
			if (nodeOrNull4 != null)
			{
				nodeOrNull4.Color = WatcherModSettings.BeautifiedCharSelectBgColor;
			}
		}
		if (nodeOrNull2 is Node2D spine)
		{
			WatcherCharSelectSpineFitter.Attach(spine);
		}
		if (nodeOrNull != null)
		{
			nodeOrNull.Visible = false;
			RemoveOrbAnimator(nodeOrNull);
		}
		WatcherTimelineLayer.Inject(watcherBg as Control);
		Log.Info($"[Watcher] char-select bg applied (gen1, community={WatcherModSettings.UseCommunitySkeleton})");
		return true;
	}

	private static void ApplyOrbAnimator(TextureRect portrait)
	{
		RemoveOrbAnimator(portrait);
		try
		{
			new WatcherOrbAnimator(portrait);
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] Failed to spawn orb animator: " + ex.Message);
		}
	}

	private static void RemoveOrbAnimator(TextureRect portrait)
	{
		Node nodeOrNull = portrait.GetNodeOrNull("WatcherOrbAnim");
		if (nodeOrNull != null && GodotObject.IsInstanceValid(nodeOrNull))
		{
			nodeOrNull.QueueFree();
		}
	}
}
[WatcherPatch(SkipOnAndroid = true, Reason = "Postfix on private NMerchantRoom.AfterRoomIsLoaded + reflects private list fields")]
[HarmonyPatch(typeof(NMerchantRoom), "AfterRoomIsLoaded")]
internal static class WatcherProphetMerchantSkinPatch
{
	private static readonly FieldInfo? PlayersField = AccessTools.Field(typeof(NMerchantRoom), "_players");

	private static readonly FieldInfo? VisualsField = AccessTools.Field(typeof(NMerchantRoom), "_playerVisuals");

	private static void Postfix(NMerchantRoom __instance)
	{
		if (!WatcherModSettings.UseCommunitySkeleton || !(PlayersField?.GetValue(__instance) is IList list) || !(VisualsField?.GetValue(__instance) is IList list2))
		{
			return;
		}
		int num = Math.Min(list.Count, list2.Count);
		for (int i = 0; i < num; i++)
		{
			if (list[i] is Player player && ProphetBridge.IsGen2(player.Character) && list2[i] is Node2D node2D)
			{
				Node2D nodeOrNull = node2D.GetNodeOrNull<Node2D>("SpineSprite");
				if (nodeOrNull != null && WatcherSkeletonHelper.ApplyCharSelectSkeleton(nodeOrNull, "res://animations/characters/watcher/prophet/watcher_skel_data.tres", "Idle"))
				{
					float beautifiedProphetMerchantScale = WatcherModSettings.BeautifiedProphetMerchantScale;
					nodeOrNull.Scale = Vector2.One * beautifiedProphetMerchantScale;
					nodeOrNull.Position += WatcherModSettings.BeautifiedProphetRigCenterOffset * beautifiedProphetMerchantScale;
					Log.Info("[Watcher] merchant skin → prophet (beautified)");
				}
			}
		}
	}
}
[HarmonyPatch(typeof(NCharacterSelectScreen), "SelectCharacter")]
internal static class WatcherCharSelectBgPatch
{
	private static readonly FieldInfo BgContainerRef = WatcherFieldAccess.Field(typeof(NCharacterSelectScreen), "_bgContainer");

	private static void Postfix(NCharacterSelectScreen __instance, NCharacterSelectButton charSelectButton, CharacterModel characterModel)
	{
		if (characterModel is Watcher && WatcherCharacterSelectBgHelper.ApplyToContainer((Control)BgContainerRef.GetValue(__instance), characterModel))
		{
			WatcherAudioHelper.PlayOneShot("res://audio/watcher/select.ogg");
		}
	}
}
[HarmonyPatch(typeof(NCharacterSelectScreen), "PlayerChanged")]
internal static class WatcherCharSelectRandomBgPatch
{
	private static readonly FieldInfo BgContainerRef = WatcherFieldAccess.Field(typeof(NCharacterSelectScreen), "_bgContainer");

	private static void Postfix(NCharacterSelectScreen __instance, StartRunLobbyPlayer player, bool isRandomCharacterResolution)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		if (isRandomCharacterResolution && player.character is Watcher && WatcherCharacterSelectBgHelper.ApplyToContainer((Control)BgContainerRef.GetValue(__instance), player.character))
		{
			WatcherAudioHelper.PlayOneShot("res://audio/watcher/select.ogg");
		}
	}
}
[HarmonyPatch(typeof(NMultiplayerLoadGameScreen), "InitializeAsHost")]
internal static class WatcherMultiplayerLoadHostBgPatch
{
	private static readonly FieldInfo BgContainerRef = WatcherFieldAccess.Field(typeof(NMultiplayerLoadGameScreen), "_bgContainer");

	private static void Postfix(NMultiplayerLoadGameScreen __instance)
	{
		WatcherCharacterSelectBgHelper.ApplyToExistingWatcherBackgrounds((Control)BgContainerRef.GetValue(__instance));
	}
}
[HarmonyPatch(typeof(NMultiplayerLoadGameScreen), "InitializeAsClient")]
internal static class WatcherMultiplayerLoadClientBgPatch
{
	private static readonly FieldInfo BgContainerRef = WatcherFieldAccess.Field(typeof(NMultiplayerLoadGameScreen), "_bgContainer");

	private static void Postfix(NMultiplayerLoadGameScreen __instance)
	{
		WatcherCharacterSelectBgHelper.ApplyToExistingWatcherBackgrounds((Control)BgContainerRef.GetValue(__instance));
	}
}
internal static class DieAnimFallbackRegistry
{
	private static readonly HashSet<ulong> _needsFallback = new HashSet<ulong>();

	public static void Register(ulong instanceId)
	{
		_needsFallback.Add(instanceId);
	}

	public static bool NeedsFallback(ulong instanceId)
	{
		return _needsFallback.Contains(instanceId);
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "Reflection-resolved MegaSpine target — native detour crash on ARM64")]
[HarmonyPatch]
internal static class MegaSpriteGetAnimStatePatch
{
	internal static MethodBase? TargetMethod()
	{
		Type type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Bindings.MegaSpine.MegaSprite");
		if (!(type != null))
		{
			return null;
		}
		return AccessTools.Method(type, "GetAnimationState");
	}

	internal static void Postfix(object __instance, object? __result)
	{
		if (__result == null || __instance == null)
		{
			return;
		}
		try
		{
			MethodInfo method = __instance.GetType().GetMethod("HasAnimation", new Type[1] { typeof(string) });
			if (!(method == null) && !(bool)(method.Invoke(__instance, new object[1] { "die" }) ?? ((object)false)) && __result.GetType().GetProperty("BoundObject")?.GetValue(__result) is GodotObject godotObject)
			{
				DieAnimFallbackRegistry.Register(godotObject.GetInstanceId());
			}
		}
		catch
		{
		}
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "Reflection-resolved MegaSpine target — native detour crash on ARM64")]
[HarmonyPatch]
internal static class DieAnimFallbackPatch
{
	internal static MethodBase? TargetMethod()
	{
		Type type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Bindings.MegaSpine.MegaAnimationState");
		if (!(type != null))
		{
			return null;
		}
		return AccessTools.Method(type, "SetAnimation");
	}

	internal static void Prefix(object __instance, ref string animationName)
	{
		if (animationName != "die" || __instance == null)
		{
			return;
		}
		try
		{
			if (__instance.GetType().GetProperty("BoundObject")?.GetValue(__instance) is GodotObject godotObject && DieAnimFallbackRegistry.NeedsFallback(godotObject.GetInstanceId()))
			{
				animationName = "Hit";
			}
		}
		catch
		{
		}
	}
}
[HarmonyPatch(typeof(ModelIdSerializationCache), "Init")]
internal static class WatcherSerializationCachePatch
{
	private static void Postfix()
	{
		System.Collections.Generic.Dictionary<string, int> dictionary = typeof(ModelIdSerializationCache).GetField("_entryNameToNetIdMap", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as System.Collections.Generic.Dictionary<string, int>;
		List<string> list = typeof(ModelIdSerializationCache).GetField("_netIdToEntryNameMap", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as List<string>;
		System.Collections.Generic.Dictionary<string, int> dictionary2 = typeof(ModelIdSerializationCache).GetField("_categoryNameToNetIdMap", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as System.Collections.Generic.Dictionary<string, int>;
		List<string> list2 = typeof(ModelIdSerializationCache).GetField("_netIdToCategoryNameMap", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as List<string>;
		if (dictionary == null || list == null || dictionary2 == null || list2 == null)
		{
			return;
		}
		bool flag = false;
		foreach (Type subtypesInMod in ReflectionHelper.GetSubtypesInMods<AbstractModel>())
		{
			ModelId id = ModelDb.GetId(subtypesInMod);
			if (!dictionary2.ContainsKey(id.Category))
			{
				dictionary2[id.Category] = list2.Count;
				list2.Add(id.Category);
				flag = true;
			}
			if (!dictionary.ContainsKey(id.Entry))
			{
				dictionary[id.Entry] = list.Count;
				list.Add(id.Entry);
				flag = true;
			}
		}
		if (flag)
		{
			PropertyInfo? property = typeof(ModelIdSerializationCache).GetProperty("EntryIdBitSize", BindingFlags.Static | BindingFlags.Public);
			PropertyInfo property2 = typeof(ModelIdSerializationCache).GetProperty("CategoryIdBitSize", BindingFlags.Static | BindingFlags.Public);
			property?.SetValue(null, Mathf.CeilToInt(Math.Log2(list.Count)));
			property2?.SetValue(null, Mathf.CeilToInt(Math.Log2(list2.Count)));
		}
	}
}
[HarmonyPatch(typeof(TheArchitect), "WinRun")]
internal static class WatcherArchitectWinRunPatch
{
	private static bool Prefix(TheArchitect __instance, ref Task __result)
	{
		if (AccessTools.Field(typeof(TheArchitect), "_dialogue")?.GetValue(__instance) != null)
		{
			return true;
		}
		if (LocalContext.IsMe(__instance.Owner))
		{
			RunManager.Instance.ActChangeSynchronizer.SetLocalPlayerReady();
		}
		__result = Task.CompletedTask;
		return false;
	}
}
[HarmonyPatch(typeof(TouchOfOrobas), "GetUpgradedStarterRelic")]
internal static class WatcherTouchOfOrobasPatch
{
	private static void Postfix(RelicModel starterRelic, ref RelicModel __result)
	{
		if (starterRelic is PureWater)
		{
			__result = ModelDb.Relic<HolyWater>();
		}
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "AfterObtained is async + accesses protected base.Owner (Cecil can't re-emit)")]
[HarmonyPatch(typeof(TouchOfOrobas), "AfterObtained")]
internal static class WatcherTouchOfOrobasAfterObtainedPatch
{
	private static Exception? Finalizer(Exception? __exception, TouchOfOrobas __instance, ref Task __result)
	{
		if (__exception != null && WatcherCompat.IsWatcherOwned(__instance) && WatcherCompat.IsExpectedSetupException(__exception))
		{
			GD.PrintErr("[Watcher] TouchOfOrobas.AfterObtained crashed: " + __exception.Message);
			__result = Task.CompletedTask;
			return null;
		}
		return __exception;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "Private static target — ARM64 native detour crash class")]
[HarmonyPatch(typeof(ArchaicTooth), "TranscendenceUpgrades", MethodType.Getter)]
internal static class WatcherArchaicToothTranscendencePatch
{
	private static void Postfix(ref System.Collections.Generic.Dictionary<ModelId, CardModel> __result)
	{
		if (__result != null)
		{
			__result[ModelDb.Card<WatcherEruption_P>().Id] = ModelDb.Card<WatcherCataclysm>();
			__result[ModelDb.Card<WatcherVigilance>().Id] = ModelDb.Card<WatcherSerenity>();
		}
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "Private instance target — ARM64 native detour crash class")]
[HarmonyPatch(typeof(ArchaicTooth), "GetTranscendenceStarterCard")]
internal static class WatcherArchaicToothRandomStarterPatch
{
	private static bool Prefix(Player player, ref CardModel? __result)
	{
		PropertyInfo property = typeof(ArchaicTooth).GetProperty("TranscendenceUpgrades", BindingFlags.Static | BindingFlags.NonPublic);
		if (property == null)
		{
			return true;
		}
		System.Collections.Generic.Dictionary<ModelId, CardModel> upgrades = property.GetValue(null) as System.Collections.Generic.Dictionary<ModelId, CardModel>;
		if (upgrades == null)
		{
			return true;
		}
		List<CardModel> list = player.Deck.Cards.Where((CardModel c) => upgrades.ContainsKey(c.Id)).ToList();
		if (list.Count <= 1)
		{
			return true;
		}
		list.Sort((CardModel a, CardModel b) => string.CompareOrdinal(a.Id.Entry, b.Id.Entry));
		Rng rng = CreateSeededRng(player.RunState.Rng, 1255938640u);
		if (rng == null)
		{
			return true;
		}
		__result = rng.NextItem(list);
		return false;
	}

	private static Rng? CreateSeededRng(object rngSet, uint salt)
	{
		try
		{
			object obj = rngSet.GetType().GetProperty("Seed")?.GetValue(rngSet);
			if (obj == null)
			{
				return null;
			}
			ulong num = Convert.ToUInt64(obj) ^ salt;
			ConstructorInfo[] constructors = typeof(Rng).GetConstructors();
			foreach (ConstructorInfo constructorInfo in constructors)
			{
				ParameterInfo[] parameters = constructorInfo.GetParameters();
				if (parameters.Length == 0)
				{
					continue;
				}
				object obj2;
				if (parameters[0].ParameterType == typeof(ulong))
				{
					obj2 = num;
				}
				else
				{
					if (!(parameters[0].ParameterType == typeof(uint)))
					{
						continue;
					}
					obj2 = (uint)num;
				}
				if (!parameters.Skip(1).Any((ParameterInfo p) => !p.HasDefaultValue))
				{
					object[] array = new object[parameters.Length];
					array[0] = obj2;
					for (int j = 1; j < parameters.Length; j++)
					{
						array[j] = parameters[j].DefaultValue;
					}
					return (Rng)constructorInfo.Invoke(array);
				}
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr("[Watcher] ArchaicTooth seeded rng failed: " + ex.Message);
		}
		return null;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "AfterObtained is async + calls private GetTranscendenceStarterCard (Cecil)")]
[HarmonyPatch(typeof(ArchaicTooth), "AfterObtained")]
internal static class WatcherArchaicToothAfterObtainedPatch
{
	private static Exception? Finalizer(Exception? __exception, ArchaicTooth __instance, ref Task __result)
	{
		if (__exception != null && WatcherCompat.IsWatcherOwned(__instance) && WatcherCompat.IsExpectedSetupException(__exception))
		{
			GD.PrintErr("[Watcher] ArchaicTooth.AfterObtained crashed: " + __exception.Message);
			__result = Task.CompletedTask;
			return null;
		}
		return __exception;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "GenerateInitialOptions Prefix calls protected RelicOption (Cecil DMD)")]
[HarmonyPatch(typeof(Orobas), "GenerateInitialOptions")]
internal static class WatcherOrobasGenerateOptionsPatch
{
	private static Exception? Finalizer(Exception? __exception, Orobas __instance, ref IReadOnlyList<EventOption> __result)
	{
		if (__exception != null && WatcherCompat.IsWatcherRun() && (WatcherCompat.IsExpectedSetupException(__exception) || __exception is MissingMemberException || __exception is TypeLoadException))
		{
			GD.PrintErr("[Watcher] Orobas.GenerateInitialOptions crashed: " + __exception.Message);
			try
			{
				MethodInfo methodInfo = AccessTools.Method(typeof(AncientEventModel), "RelicOption", new Type[1] { typeof(RelicModel) });
				if (methodInfo == null)
				{
					return __exception;
				}
				RelicModel[] obj = new RelicModel[3]
				{
					ModelDb.Relic<ElectricShrymp>(),
					ModelDb.Relic<GlassEye>(),
					ModelDb.Relic<SandCastle>()
				};
				List<EventOption> list = new List<EventOption>();
				RelicModel[] array = obj;
				foreach (RelicModel relicModel in array)
				{
					if (methodInfo.Invoke(__instance, new object[1] { relicModel.ToMutable() }) is EventOption item)
					{
						list.Add(item);
					}
				}
				if (list.Count > 0)
				{
					__result = list;
					GD.Print($"[Watcher] Orobas fallback: generated {list.Count} relic options");
					return null;
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr("[Watcher] Orobas fallback failed: " + ex.Message);
			}
		}
		return __exception;
	}
}
[HarmonyPatch(typeof(HandPosHelper), "GetPosition")]
internal static class HandPosOverflowPositionPatch
{
	private static readonly Vector2[] Max = new Vector2[10]
	{
		new Vector2(-610f, 38f),
		new Vector2(-472f, 5f),
		new Vector2(-340f, -21f),
		new Vector2(-200f, -41f),
		new Vector2(-64f, -50f),
		new Vector2(64f, -50f),
		new Vector2(200f, -41f),
		new Vector2(340f, -21f),
		new Vector2(472f, 5f),
		new Vector2(610f, 38f)
	};

	private static bool Prefix(int handSize, int cardIndex, ref Vector2 __result)
	{
		if (handSize <= 10)
		{
			return true;
		}
		float num = (float)cardIndex / (float)(handSize - 1) * 9f;
		int num2 = Math.Clamp((int)num, 0, 8);
		float weight = num - (float)num2;
		__result = Max[num2].Lerp(Max[num2 + 1], weight);
		return false;
	}
}
[HarmonyPatch(typeof(HandPosHelper), "GetAngle")]
internal static class HandPosOverflowAnglePatch
{
	private static readonly float[] Max = new float[10] { -15f, -12f, -9f, -6f, -3f, 3f, 6f, 9f, 12f, 15f };

	private static bool Prefix(int handSize, int cardIndex, ref float __result)
	{
		if (handSize <= 10)
		{
			return true;
		}
		float num = (float)cardIndex / (float)(handSize - 1) * 9f;
		int num2 = Math.Clamp((int)num, 0, 8);
		float num3 = num - (float)num2;
		__result = Max[num2] + (Max[num2 + 1] - Max[num2]) * num3;
		return false;
	}
}
[HarmonyPatch(typeof(PowerModel), "AddDumbVariablesToDescription")]
internal static class PowerDumbAmountPatch
{
	private static readonly Assembly WatcherAssembly = typeof(PowerDumbAmountPatch).Assembly;

	private static void Postfix(PowerModel __instance, LocString description)
	{
		if (!(__instance.GetType().Assembly != WatcherAssembly))
		{
			description.Add("Amount", __instance.Amount);
		}
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "GenerateInitialOptions Prefix calls protected RelicOption (Cecil DMD)")]
[HarmonyPatch(typeof(Darv), "GenerateInitialOptions")]
internal static class WatcherDarvRelicPatch
{
	private static bool _injected;

	private static bool Prefix(Darv __instance, ref IReadOnlyList<EventOption> __result)
	{
		InjectVioletLotusSet();
		if (!(__instance.Owner?.Character is Watcher))
		{
			return true;
		}
		try
		{
			List<EventOption> list = BuildSafeRelicOptions(__instance, includeVioletLotus: true);
			if (list.Count > 0)
			{
				__result = list;
				return false;
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr("[Watcher] Darv watcher options failed: " + ex.Message);
		}
		return true;
	}

	private static void InjectVioletLotusSet()
	{
		if (_injected)
		{
			return;
		}
		try
		{
			if (!(AccessTools.Field(typeof(Darv), "_validRelicSets")?.GetValue(null) is IList list))
			{
				return;
			}
			Type nestedType = typeof(Darv).GetNestedType("ValidRelicSet", BindingFlags.NonPublic);
			if (nestedType == null)
			{
				return;
			}
			Type type = typeof(Func<, >).MakeGenericType(typeof(Player), typeof(bool));
			ConstructorInfo constructor = nestedType.GetConstructor(new Type[2]
			{
				type,
				typeof(RelicModel[])
			});
			if (!(constructor == null))
			{
				Func<Player, bool> func = (Player owner) => owner?.Character is Watcher;
				object value = constructor.Invoke(new object[2]
				{
					func,
					new RelicModel[1] { ModelDb.Relic<VioletLotus>() }
				});
				list.Add(value);
				_injected = true;
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr("[Watcher] Darv VioletLotus injection failed: " + ex.Message);
		}
	}

	private static List<EventOption> BuildSafeRelicOptions(Darv instance, bool includeVioletLotus)
	{
		MethodInfo methodInfo = AccessTools.Method(typeof(AncientEventModel), "RelicOption", new Type[3]
		{
			typeof(RelicModel),
			typeof(string),
			typeof(string)
		});
		if (methodInfo == null)
		{
			return new List<EventOption>();
		}
		List<RelicModel> list = new List<RelicModel>
		{
			ModelDb.Relic<Astrolabe>(),
			ModelDb.Relic<BlackStar>(),
			ModelDb.Relic<CallingBell>(),
			ModelDb.Relic<EmptyCage>(),
			ModelDb.Relic<RunicPyramid>(),
			ModelDb.Relic<SneckoEye>()
		};
		IRunState runState = instance.Owner?.RunState;
		if (runState != null && !runState.Modifiers.Any((ModifierModel m) => m.ClearsPlayerDeck))
		{
			list.Add(ModelDb.Relic<PandorasBox>());
		}
		if (runState != null && runState.CurrentActIndex == 1)
		{
			RelicModel relicModel = instance.Rng.NextItem(new RelicModel[2]
			{
				ModelDb.Relic<Ectoplasm>(),
				ModelDb.Relic<Sozu>()
			});
			if (relicModel != null)
			{
				list.Add(relicModel);
			}
		}
		else if (runState != null && runState.CurrentActIndex == 2)
		{
			RelicModel relicModel2 = instance.Rng.NextItem(new RelicModel[2]
			{
				ModelDb.Relic<PhilosophersStone>(),
				ModelDb.Relic<VelvetChoker>()
			});
			if (relicModel2 != null)
			{
				list.Add(relicModel2);
			}
		}
		if (includeVioletLotus)
		{
			list.Add(ModelDb.Relic<VioletLotus>());
		}
		instance.Rng.Shuffle(list);
		List<EventOption> list2 = new List<EventOption>();
		foreach (RelicModel item2 in list.Take(3))
		{
			if (methodInfo.Invoke(instance, new object[3]
			{
				item2.ToMutable(),
				"INITIAL",
				null
			}) is EventOption item)
			{
				list2.Add(item);
			}
		}
		return list2;
	}

	private static Exception? Finalizer(Exception? __exception, Darv __instance, ref IReadOnlyList<EventOption> __result)
	{
		if (__exception != null && __instance.Owner?.Character is Watcher && WatcherCompat.IsExpectedSetupException(__exception))
		{
			GD.PrintErr("[Watcher] Darv.GenerateInitialOptions crashed: " + __exception.Message);
			try
			{
				List<EventOption> list = BuildSafeRelicOptions(__instance, includeVioletLotus: true);
				if (list.Count > 0)
				{
					__result = list;
					GD.Print($"[Watcher] Darv fallback: generated {list.Count} relic options");
					return null;
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr("[Watcher] Darv fallback options also failed: " + ex.Message);
			}
		}
		return __exception;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "CharacterModel Prefix getter on private ArmPointingPath (Cecil DMD)")]
[HarmonyPatch(typeof(CharacterModel), "ArmPointingTexturePath", MethodType.Getter)]
internal static class WatcherArmPointingPathPatch
{
	private static bool Prefix(CharacterModel __instance, ref string __result)
	{
		if (!(__instance is Watcher))
		{
			return true;
		}
		__result = "res://images/ui/hands/multiplayer_hand_watcher_point.png";
		return false;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "CharacterModel Prefix getter on private ArmRockPath (Cecil DMD)")]
[HarmonyPatch(typeof(CharacterModel), "ArmRockTexturePath", MethodType.Getter)]
internal static class WatcherArmRockPathPatch
{
	private static bool Prefix(CharacterModel __instance, ref string __result)
	{
		if (!(__instance is Watcher))
		{
			return true;
		}
		__result = "res://images/ui/hands/multiplayer_hand_watcher_rock.png";
		return false;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "CharacterModel Prefix getter on private ArmPaperPath (Cecil DMD)")]
[HarmonyPatch(typeof(CharacterModel), "ArmPaperTexturePath", MethodType.Getter)]
internal static class WatcherArmPaperPathPatch
{
	private static bool Prefix(CharacterModel __instance, ref string __result)
	{
		if (!(__instance is Watcher))
		{
			return true;
		}
		__result = "res://images/ui/hands/multiplayer_hand_watcher_paper.png";
		return false;
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "CharacterModel Prefix getter on private ArmScissorsPath (Cecil DMD)")]
[HarmonyPatch(typeof(CharacterModel), "ArmScissorsTexturePath", MethodType.Getter)]
internal static class WatcherArmScissorsPathPatch
{
	private static bool Prefix(CharacterModel __instance, ref string __result)
	{
		if (!(__instance is Watcher))
		{
			return true;
		}
		__result = "res://images/ui/hands/multiplayer_hand_watcher_scissors.png";
		return false;
	}
}
internal static class WatcherCardLibraryInjector
{
	private static void FixOwnerRecursive(Node root, Node owner)
	{
		foreach (Node child in root.GetChildren())
		{
			child.Owner = owner;
			FixOwnerRecursive(child, owner);
		}
	}

	public static void Inject(NCardLibrary instance)
	{
		if (instance.FindChild("WatcherPool", recursive: true, owned: false) != null)
		{
			return;
		}
		CharacterModel byIdOrNull = ModelDb.GetByIdOrNull<CharacterModel>(ModelDb.GetId(typeof(Watcher)));
		CharacterModel characterModel = ProphetBridge.Gen2Character();
		if (byIdOrNull == null || !(AccessTools.Field(typeof(NCardLibrary), "_necrobinderFilter")?.GetValue(instance) is NCardPoolFilter template))
		{
			return;
		}
		System.Collections.Generic.Dictionary<NCardPoolFilter, Func<CardModel, bool>> dictionary = AccessTools.Field(typeof(NCardLibrary), "_poolFilters")?.GetValue(instance) as System.Collections.Generic.Dictionary<NCardPoolFilter, Func<CardModel, bool>>;
		System.Collections.Generic.Dictionary<CharacterModel, NCardPoolFilter> dictionary2 = AccessTools.Field(typeof(NCardLibrary), "_cardPoolFilters")?.GetValue(instance) as System.Collections.Generic.Dictionary<CharacterModel, NCardPoolFilter>;
		if (dictionary == null || dictionary2 == null)
		{
			return;
		}
		MethodInfo updateMethod = AccessTools.Method(typeof(NCardLibrary), "UpdateCardPoolFilter");
		FieldInfo lastHoveredField = AccessTools.Field(typeof(NCardLibrary), "_lastHoveredControl");
		Texture2D texture2D = WatcherTextureHelper.LoadTexture("res://images/ui/top_panel/character_icon_watcher.png");
		Texture2D icon = texture2D;
		if (texture2D != null)
		{
			Image image = WatcherKnowFateHud.BakePurpleImage(texture2D, new Color("EFC04A"));
			if (image != null)
			{
				try
				{
					icon = ImageTexture.CreateFromImage(image);
				}
				catch (Exception ex)
				{
					Log.Error("[Watcher] Gen2 pool icon bake failed: " + ex.Message);
				}
			}
		}
		WatcherCardPool watcherCardPool = ModelDb.CardPool<WatcherCardPool>();
		CardPoolModel cardPoolModel = characterModel?.CardPool;
		HashSet<ModelId> gen1Ids = watcherCardPool.AllCardIds.ToHashSet();
		HashSet<ModelId> gen2Ids = cardPoolModel?.AllCardIds.ToHashSet();
		NCardPoolFilter nCardPoolFilter = CreatePoolFilter(template, "WatcherPool", texture2D, shimmer: false, new LocString("characters", ModelDb.GetId(typeof(Watcher)).Entry + ".title"));
		RegisterPoolFilter(instance, nCardPoolFilter, updateMethod, lastHoveredField, dictionary, (CardModel c) => gen1Ids.Contains(c.Id));
		dictionary2[byIdOrNull] = nCardPoolFilter;
		if (characterModel != null && gen2Ids != null)
		{
			NCardPoolFilter nCardPoolFilter2 = CreatePoolFilter(nCardPoolFilter, "WatcherV2Pool", icon, shimmer: true, new LocString("characters", characterModel.Id.Entry + ".title"));
			RegisterPoolFilter(instance, nCardPoolFilter2, updateMethod, lastHoveredField, dictionary, (CardModel c) => gen2Ids.Contains(c.Id));
			dictionary2[characterModel] = nCardPoolFilter2;
		}
	}

	private static NCardPoolFilter CreatePoolFilter(NCardPoolFilter template, string name, Texture2D? icon, bool shimmer, LocString? loc = null)
	{
		NCardPoolFilter nCardPoolFilter = (NCardPoolFilter)template.Duplicate(6);
		nCardPoolFilter.Name = name;
		FixOwnerRecursive(nCardPoolFilter, nCardPoolFilter);
		Control nodeOrNull = nCardPoolFilter.GetNodeOrNull<Control>("Image");
		if (nodeOrNull != null && nodeOrNull.GetMaterial() is ShaderMaterial shaderMaterial)
		{
			nodeOrNull.Material = (ShaderMaterial)shaderMaterial.Duplicate();
		}
		Node parent = template.GetParent();
		parent.AddChild(nCardPoolFilter, forceReadableName: false, Node.InternalMode.Disabled);
		parent.MoveChild(nCardPoolFilter, template.GetIndex() + 1);
		if (nCardPoolFilter.GetNodeOrNull<Control>("Image") is TextureRect textureRect)
		{
			if (icon != null)
			{
				textureRect.Texture = icon;
			}
			if (shimmer)
			{
				WatcherShimmerOverlay.AttachTo(textureRect);
			}
		}
		if (loc != null)
		{
			nCardPoolFilter.Loc = loc;
		}
		nCardPoolFilter.Visible = true;
		return nCardPoolFilter;
	}

	private static void RegisterPoolFilter(NCardLibrary instance, NCardPoolFilter filter, MethodInfo? updateMethod, FieldInfo? lastHoveredField, System.Collections.Generic.Dictionary<NCardPoolFilter, Func<CardModel, bool>> poolFilters, Func<CardModel, bool> predicate)
	{
		poolFilters[filter] = predicate;
		if (updateMethod != null)
		{
			filter.Connect("Toggled", Callable.From(delegate(NCardPoolFilter f)
			{
				updateMethod.Invoke(instance, new object[1] { f });
			}));
		}
		if (lastHoveredField != null)
		{
			filter.Connect(Control.SignalName.FocusEntered, Callable.From(delegate
			{
				lastHoveredField.SetValue(instance, filter);
			}));
		}
	}

	public static void InstallSceneTreeListener()
	{
		if (Engine.GetMainLoop() is SceneTree sceneTree)
		{
			sceneTree.NodeAdded += OnNodeAdded;
		}
	}

	private static void OnNodeAdded(Node node)
	{
		NCardLibrary cardLibrary = node as NCardLibrary;
		if (cardLibrary == null)
		{
			return;
		}
		cardLibrary.CallDeferred("set", "_deferred_watcher_inject", true);
		cardLibrary.GetTree().CreateTimer(0.1).Timeout += delegate
		{
			if (GodotObject.IsInstanceValid(cardLibrary))
			{
				Inject(cardLibrary);
			}
		};
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "_Ready Postfix calls protected NSubmenu.ConnectSignals (Cecil DMD); SceneTreeListener used as Android fallback")]
[HarmonyPatch(typeof(NCardLibrary), "_Ready")]
internal static class WatcherCardLibraryPatch
{
	private static void Postfix(NCardLibrary __instance)
	{
		WatcherCardLibraryInjector.Inject(__instance);
	}
}
internal static class WatcherShimmerOverlay
{
	private const string OverlayName = "WatcherShimmerOverlay";

	private const string ShaderCode = "shader_type canvas_item;\r\nrender_mode blend_add;\r\n\r\nuniform float speed : hint_range(0.0, 5.0) = 0.6;\r\nuniform float band_width : hint_range(0.05, 1.0) = 0.35;\r\nuniform float intensity : hint_range(0.0, 2.0) = 0.7;\r\nuniform vec4 tint : source_color = vec4(0.78, 0.55, 1.0, 1.0);\r\n\r\nvoid fragment() {\r\n    vec4 tex = texture(TEXTURE, UV);\r\n    float phase = fract(TIME * speed * 0.25);\r\n    float pos = phase * (1.0 + band_width * 2.0) - band_width;\r\n    float d = (UV.x + UV.y) * 0.5 - pos;\r\n    float band = exp(-pow(d / band_width, 2.0) * 4.0);\r\n    float mask = step(0.01, tex.a);\r\n    COLOR = vec4(tint.rgb * band * intensity * mask, tex.a * band * mask);\r\n}";

	public static void AttachTo(TextureRect? target)
	{
		if (target != null && target.FindChild("WatcherShimmerOverlay", recursive: false, owned: false) == null && target.Texture != null)
		{
			Shader shader = new Shader
			{
				Code = "shader_type canvas_item;\r\nrender_mode blend_add;\r\n\r\nuniform float speed : hint_range(0.0, 5.0) = 0.6;\r\nuniform float band_width : hint_range(0.05, 1.0) = 0.35;\r\nuniform float intensity : hint_range(0.0, 2.0) = 0.7;\r\nuniform vec4 tint : source_color = vec4(0.78, 0.55, 1.0, 1.0);\r\n\r\nvoid fragment() {\r\n    vec4 tex = texture(TEXTURE, UV);\r\n    float phase = fract(TIME * speed * 0.25);\r\n    float pos = phase * (1.0 + band_width * 2.0) - band_width;\r\n    float d = (UV.x + UV.y) * 0.5 - pos;\r\n    float band = exp(-pow(d / band_width, 2.0) * 4.0);\r\n    float mask = step(0.01, tex.a);\r\n    COLOR = vec4(tint.rgb * band * intensity * mask, tex.a * band * mask);\r\n}"
			};
			ShaderMaterial material = new ShaderMaterial
			{
				Shader = shader
			};
			TextureRect textureRect = new TextureRect
			{
				Name = "WatcherShimmerOverlay",
				Texture = target.Texture,
				StretchMode = target.StretchMode,
				ExpandMode = target.ExpandMode,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				Material = material
			};
			target.AddChild(textureRect, forceReadableName: false, Node.InternalMode.Disabled);
			textureRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		}
	}
}
[HarmonyPatch(typeof(NGeneralStatsGrid), "LoadStats")]
internal static class WatcherStatsGridPatch
{
	private static readonly MethodInfo? CreateCharSectionMethod = AccessTools.Method(typeof(NGeneralStatsGrid), "CreateCharacterSection");

	private static void Postfix(NGeneralStatsGrid __instance)
	{
		if (!(CreateCharSectionMethod == null))
		{
			CharacterModel byIdOrNull = ModelDb.GetByIdOrNull<CharacterModel>(ModelDb.GetId(typeof(Watcher)));
			if (byIdOrNull != null)
			{
				ProgressState progress = SaveManager.Instance.Progress;
				CreateCharSectionMethod.Invoke(__instance, new object[2] { progress, byIdOrNull.Id });
			}
		}
	}
}
[HarmonyPatch(typeof(PaelsEye), "AfterTakingExtraTurn")]
internal static class WatcherPaelsEyeExtraTurnPatch
{
	private static bool Prefix(Player player, ref Task __result)
	{
		if (player != null && WatcherExtraTurnPower.ConsumeModGrantedExtraTurn(player))
		{
			__result = Task.CompletedTask;
			return false;
		}
		return true;
	}
}
[HarmonyPatch(typeof(PaelsEye), "AfterSideTurnStart")]
internal static class WatcherPaelsEyeTurnStartClearPatch
{
	private static void Postfix(PaelsEye __instance)
	{
		if (__instance.Owner != null)
		{
			WatcherExtraTurnPower.ConsumeModGrantedExtraTurn(__instance.Owner);
		}
	}
}
internal static class WatcherInspectCardArtToggleInjector
{
	private const string ToggleTickboxName = "WatcherArtTickbox";

	private const string SetupMarker = "_watcher_art_toggle_setup";

	private static readonly FieldInfo? CardsField = AccessTools.Field(typeof(NInspectCardScreen), "_cards");

	private static readonly FieldInfo? IndexField = AccessTools.Field(typeof(NInspectCardScreen), "_index");

	private static void FixOwnerRecursive(Node root, Node owner)
	{
		foreach (Node child in root.GetChildren())
		{
			child.Owner = owner;
			FixOwnerRecursive(child, owner);
		}
	}

	private static Node? FindLabel(NTickbox tickbox)
	{
		Node node = tickbox.FindChild("ShowUpgradeLabel", recursive: true, owned: false);
		if (node != null)
		{
			return node;
		}
		foreach (Node child in tickbox.GetChildren())
		{
			if (child is MegaLabel)
			{
				return child;
			}
		}
		return null;
	}

	private static NTickbox? EnsureToggle(NInspectCardScreen screen)
	{
		if (screen.FindChild("WatcherArtTickbox", recursive: true, owned: false) is NTickbox result)
		{
			return result;
		}
		NTickbox nodeOrNull = screen.GetNodeOrNull<NTickbox>("%Upgrade");
		if (nodeOrNull == null)
		{
			return null;
		}
		NTickbox nTickbox = (NTickbox)nodeOrNull.Duplicate(6);
		nTickbox.Name = "WatcherArtTickbox";
		FixOwnerRecursive(nTickbox, nTickbox);
		float num = 24f;
		Vector2 globalPosition = new Vector2(nodeOrNull.GlobalPosition.X + nodeOrNull.Size.X + num, nodeOrNull.GlobalPosition.Y);
		screen.AddChild(nTickbox, forceReadableName: false, Node.InternalMode.Disabled);
		nTickbox.GlobalPosition = globalPosition;
		Node node = FindLabel(nTickbox);
		if (node is MegaLabel megaLabel)
		{
			megaLabel.SetTextAutoSize("手绘画风");
		}
		else if (node is Label label)
		{
			label.Text = "手绘画风";
		}
		return nTickbox;
	}

	public static void UpdateToggle(NInspectCardScreen screen)
	{
		List<CardModel> list = CardsField?.GetValue(screen) as List<CardModel>;
		int num = (int)(IndexField?.GetValue(screen) ?? ((object)(-1)));
		Log.Info($"[Watcher] ArtToggle: UpdateToggle cards={list?.Count} index={num}");
		if (list == null || num < 0 || num >= list.Count)
		{
			return;
		}
		CardModel card = list[num];
		bool flag = card.Pool is WatcherCardPool;
		NTickbox nTickbox = EnsureToggle(screen);
		if (nTickbox == null)
		{
			return;
		}
		if (!flag || !WatcherCardArtSettings.HasBetaArt(card))
		{
			nTickbox.Visible = false;
			return;
		}
		nTickbox.Visible = true;
		nTickbox.IsTicked = WatcherCardArtSettings.IsHandDrawn(card);
		foreach (Dictionary signalConnection in nTickbox.GetSignalConnectionList(NTickbox.SignalName.Toggled))
		{
			try
			{
				Callable callable = (Callable)signalConnection["callable"];
				if (nTickbox.IsConnected(NTickbox.SignalName.Toggled, callable))
				{
					nTickbox.Disconnect(NTickbox.SignalName.Toggled, callable);
				}
			}
			catch
			{
			}
		}
		nTickbox.Connect(NTickbox.SignalName.Toggled, Callable.From<NTickbox>(delegate
		{
			WatcherCardArtSettings.ToggleCard(card);
			AccessTools.Method(typeof(NInspectCardScreen), "UpdateCardDisplay")?.Invoke(screen, null);
			RefreshMatchingCards(screen.GetTree().Root, card);
		}));
	}

	private static void RefreshMatchingCards(Node root, CardModel targetCard)
	{
		foreach (Node child in root.GetChildren())
		{
			if (child is NCard nCard && nCard.Model?.Id == targetCard.Id)
			{
				nCard.Model = nCard.Model;
			}
			RefreshMatchingCards(child, targetCard);
		}
	}

	public static void InstallSceneTreeListener()
	{
		SceneTree sceneTree = Engine.GetMainLoop() as SceneTree;
		Log.Info($"[Watcher] ArtToggle: InstallSceneTreeListener tree={sceneTree}");
		if (sceneTree != null)
		{
			sceneTree.NodeAdded += OnNodeAdded;
			Log.Info("[Watcher] ArtToggle: NodeAdded listener installed");
		}
	}

	private static void OnNodeAdded(Node node)
	{
		NInspectCardScreen screen = node as NInspectCardScreen;
		if (screen == null)
		{
			return;
		}
		Log.Info("[Watcher] ArtToggle: NInspectCardScreen detected via NodeAdded");
		screen.GetTree().CreateTimer(0.1).Timeout += delegate
		{
			if (GodotObject.IsInstanceValid(screen))
			{
				SetupSignalHooks(screen);
			}
		};
	}

	private static void SetupSignalHooks(NInspectCardScreen screen)
	{
		Log.Info("[Watcher] ArtToggle: SetupSignalHooks called");
		if (screen.HasMeta("_watcher_art_toggle_setup"))
		{
			return;
		}
		screen.SetMeta("_watcher_art_toggle_setup", true);
		Log.Info("[Watcher] ArtToggle: Signal hooks set up OK");
		screen.Connect(CanvasItem.SignalName.VisibilityChanged, Callable.From(delegate
		{
			if (screen.Visible)
			{
				Callable.From(delegate
				{
					UpdateToggle(screen);
				}).CallDeferred();
			}
		}));
		Control control = AccessTools.Field(typeof(NInspectCardScreen), "_leftButton")?.GetValue(screen) as Control;
		Control control2 = AccessTools.Field(typeof(NInspectCardScreen), "_rightButton")?.GetValue(screen) as Control;
		control?.Connect("Released", Callable.From<Control>(delegate
		{
			Callable.From(delegate
			{
				UpdateToggle(screen);
			}).CallDeferred();
		}));
		control2?.Connect("Released", Callable.From<Control>(delegate
		{
			Callable.From(delegate
			{
				UpdateToggle(screen);
			}).CallDeferred();
		}));
		if (screen.Visible)
		{
			Callable.From(delegate
			{
				UpdateToggle(screen);
			}).CallDeferred();
		}
	}
}
[WatcherPatch(SkipOnAndroid = false, Reason = "NInspectCardScreen.SetCard cosmetic patch; SceneTreeListener used as Android fallback")]
[HarmonyPatch(typeof(NInspectCardScreen), "SetCard")]
internal static class WatcherInspectCardArtTogglePatch
{
	private static void Postfix(NInspectCardScreen __instance)
	{
		WatcherInspectCardArtToggleInjector.UpdateToggle(__instance);
	}
}
internal static class CataclysmVfxRandomizer
{
	private static readonly string[] VfxPool = new string[18]
	{
		"vfx/vfx_attack_slash", "vfx/vfx_attack_blunt", "vfx/vfx_attack_lightning", "vfx/vfx_bite", "vfx/vfx_bloody_impact", "vfx/vfx_chain", "vfx/vfx_flying_slash", "vfx/vfx_giant_horizontal_slash", "vfx/vfx_dagger_throw", "vfx/vfx_dagger_spray",
		"vfx/vfx_dramatic_stab", "vfx/vfx_rock_shatter", "vfx/vfx_scratch", "vfx/vfx_sandy_impact", "vfx/vfx_slime_impact", "vfx/vfx_thrash", "vfx/vfx_heavy_blunt", "vfx/vfx_starry_impact"
	};

	private static readonly Random Rng = new Random();

	[ThreadStatic]
	private static int _depth;

	public static bool Active => _depth > 0;

	public static void Enter()
	{
		_depth++;
	}

	public static void Exit()
	{
		if (_depth > 0)
		{
			_depth--;
		}
	}

	public static string NextVfx()
	{
		return VfxPool[Rng.Next(VfxPool.Length)];
	}
}
[HarmonyPatch(typeof(AttackCommand), "WithHitFx")]
internal static class AttackCommandWithHitFxPatch
{
	private static void Prefix(ref string? vfx)
	{
		if (CataclysmVfxRandomizer.Active && vfx != null)
		{
			vfx = CataclysmVfxRandomizer.NextVfx();
		}
	}
}
public sealed class Ambrosia : PotionModel
{
	private readonly Color _tint = new Color("e8d28a");

	public override PotionRarity Rarity => PotionRarity.Rare;

	public override PotionUsage Usage => PotionUsage.CombatOnly;

	public override TargetType TargetType => TargetType.Self;

	public override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		WatcherHoverTips.Stance,
		HoverTipFactory.FromPower<Divinity>(null)
	};

	protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
	{
		NCombatRoom.Instance?.PlaySplashVfx(base.Owner.Creature, _tint);
		await WatcherCombatHelper.EnterDivinity(base.Owner, null);
	}
}
public sealed class BottledMiracle : PotionModel
{
	public override PotionRarity Rarity => PotionRarity.Common;

	public override PotionUsage Usage => PotionUsage.CombatOnly;

	public override TargetType TargetType => TargetType.Self;

	protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
	{
		CombatState combat = WatcherCreatureCompat.GetCombatState(base.Owner.Creature);
		if (combat != null)
		{
			for (int i = 0; i < 2; i++)
			{
				await WatcherCardPileCompat.AddGeneratedCardToCombat(combat.CreateCard<WatcherMiracle>(base.Owner), PileType.Hand, addedByPlayer: true);
			}
		}
	}
}
public sealed class StancePotion : PotionModel
{
	public override PotionRarity Rarity => PotionRarity.Uncommon;

	public override PotionUsage Usage => PotionUsage.CombatOnly;

	public override TargetType TargetType => TargetType.Self;

	protected override async Task OnUse(PlayerChoiceContext choiceContext, Creature? target)
	{
		CombatState combatState = WatcherCreatureCompat.GetCombatState(base.Owner.Creature);
		if (combatState != null)
		{
			CardModel calmChoice = combatState.CreateCard<WatcherStancePotionCalmChoice>(base.Owner);
			CardModel wrathChoice = combatState.CreateCard<WatcherStancePotionWrathChoice>(base.Owner);
			CardModel cardModel = await WatcherCombatHelper.ChooseOne(choiceContext, base.Owner, new List<CardModel> { calmChoice, wrathChoice }, base.SelectionScreenPrompt);
			if (cardModel == calmChoice)
			{
				await WatcherCombatHelper.EnterCalm(base.Owner, null);
			}
			else if (cardModel == wrathChoice)
			{
				await WatcherCombatHelper.EnterWrath(base.Owner, null);
			}
		}
	}
}
public sealed class WatcherStancePotionCalmChoice : WatcherV2ChoiceTokenBase
{
	public override string PortraitPath => "res://images/powers/calm.png";

	public override string BetaPortraitPath => PortraitPath;
}
public sealed class WatcherStancePotionWrathChoice : WatcherV2ChoiceTokenBase
{
	public override string PortraitPath => "res://images/powers/wrath.png";

	public override string BetaPortraitPath => PortraitPath;
}
internal static class WatcherPowerCmdCompat
{
	private static readonly MethodInfo? ApplyTargetWithChoiceContext = FindGenericPowerMethod("Apply", typeof(PlayerChoiceContext), typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool));

	private static readonly MethodInfo? ApplyTargetLegacy = FindGenericPowerMethod("Apply", typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool));

	private static readonly MethodInfo? ApplyTargetsWithChoiceContext = FindGenericPowerMethod("Apply", typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool));

	private static readonly MethodInfo? ApplyTargetsLegacy = FindGenericPowerMethod("Apply", typeof(IEnumerable<Creature>), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool));

	private static readonly MethodInfo? ModifyAmountWithChoiceContext = FindPowerMethod("ModifyAmount", typeof(PlayerChoiceContext), typeof(PowerModel), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool));

	private static readonly MethodInfo? ModifyAmountLegacy = FindPowerMethod("ModifyAmount", typeof(PowerModel), typeof(decimal), typeof(Creature), typeof(CardModel), typeof(bool));

	private static readonly MethodInfo? SetAmountLegacy = FindGenericPowerMethod("SetAmount", typeof(Creature), typeof(decimal), typeof(Creature), typeof(CardModel));

	public static Task<T?> Apply<T>(Creature target, decimal amount, Creature? applier, CardModel? cardSource, bool silent = false) where T : PowerModel
	{
		if (ApplyTargetWithChoiceContext != null)
		{
			return Invoke<Task<T>>(ApplyTargetWithChoiceContext.MakeGenericMethod(typeof(T)), new object[6]
			{
				new ThrowingPlayerChoiceContext(),
				target,
				amount,
				applier,
				cardSource,
				silent
			});
		}
		if (ApplyTargetLegacy != null)
		{
			return Invoke<Task<T>>(ApplyTargetLegacy.MakeGenericMethod(typeof(T)), new object[5] { target, amount, applier, cardSource, silent });
		}
		throw MissingPowerMethod("Apply");
	}

	public static Task<IReadOnlyList<T>> Apply<T>(IEnumerable<Creature> targets, decimal amount, Creature? applier, CardModel? cardSource, bool silent = false) where T : PowerModel
	{
		if (ApplyTargetsWithChoiceContext != null)
		{
			return Invoke<Task<IReadOnlyList<T>>>(ApplyTargetsWithChoiceContext.MakeGenericMethod(typeof(T)), new object[6]
			{
				new ThrowingPlayerChoiceContext(),
				targets,
				amount,
				applier,
				cardSource,
				silent
			});
		}
		if (ApplyTargetsLegacy != null)
		{
			return Invoke<Task<IReadOnlyList<T>>>(ApplyTargetsLegacy.MakeGenericMethod(typeof(T)), new object[5] { targets, amount, applier, cardSource, silent });
		}
		throw MissingPowerMethod("Apply");
	}

	public static Task<int> ModifyAmount(PowerModel power, decimal offset, Creature? applier, CardModel? cardSource, bool silent = false)
	{
		if (ModifyAmountWithChoiceContext != null)
		{
			return Invoke<Task<int>>(ModifyAmountWithChoiceContext, new object[6]
			{
				new ThrowingPlayerChoiceContext(),
				power,
				offset,
				applier,
				cardSource,
				silent
			});
		}
		if (ModifyAmountLegacy != null)
		{
			return Invoke<Task<int>>(ModifyAmountLegacy, new object[5] { power, offset, applier, cardSource, silent });
		}
		throw MissingPowerMethod("ModifyAmount");
	}

	public static Task<T?> SetAmount<T>(Creature target, decimal amount, Creature? applier, CardModel? cardSource) where T : PowerModel
	{
		if (SetAmountLegacy != null)
		{
			return Invoke<Task<T>>(SetAmountLegacy.MakeGenericMethod(typeof(T)), new object[4] { target, amount, applier, cardSource });
		}
		return SetAmountViaModify<T>(target, amount, applier, cardSource);
	}

	private static async Task<T?> SetAmountViaModify<T>(Creature target, decimal amount, Creature? applier, CardModel? cardSource) where T : PowerModel
	{
		T existingPower = target.GetPower<T>();
		if (existingPower == null)
		{
			return await Apply<T>(target, amount, applier, cardSource);
		}
		await ModifyAmount(existingPower, amount - (decimal)existingPower.Amount, applier, cardSource);
		return existingPower;
	}

	private static T Invoke<T>(MethodInfo method, params object?[] args)
	{
		object obj = method.Invoke(null, args);
		if (obj is T)
		{
			return (T)obj;
		}
		throw new InvalidOperationException($"{method.DeclaringType?.FullName}.{method.Name} returned unexpected type {obj?.GetType().FullName ?? "<null>"}.");
	}

	private static MethodInfo? FindGenericPowerMethod(string methodName, params Type[] parameterTypes)
	{
		return typeof(PowerCmd).GetMethods(BindingFlags.Static | BindingFlags.Public).FirstOrDefault((MethodInfo method) => method.Name == methodName && method.IsGenericMethodDefinition && ParameterTypesMatch(method, parameterTypes));
	}

	private static MethodInfo? FindPowerMethod(string methodName, params Type[] parameterTypes)
	{
		return typeof(PowerCmd).GetMethods(BindingFlags.Static | BindingFlags.Public).FirstOrDefault((MethodInfo method) => method.Name == methodName && !method.IsGenericMethodDefinition && ParameterTypesMatch(method, parameterTypes));
	}

	private static bool ParameterTypesMatch(MethodInfo method, IReadOnlyList<Type> expectedTypes)
	{
		ParameterInfo[] parameters = method.GetParameters();
		if (parameters.Length != expectedTypes.Count)
		{
			return false;
		}
		for (int i = 0; i < parameters.Length; i++)
		{
			if (parameters[i].ParameterType != expectedTypes[i])
			{
				return false;
			}
		}
		return true;
	}

	private static MissingMethodException MissingPowerMethod(string methodName)
	{
		return new MissingMethodException(typeof(PowerCmd).FullName, methodName);
	}
}
public sealed class ProphecyContext
{
	public CardModel? Source { get; init; }

	public int CardsDiscarded { get; init; }

	public Creature? AffectedEnemy { get; init; }

	public bool ChangedIntent { get; init; }

	public bool FromScry { get; init; }

	public IReadOnlyList<CardModel> PeekedCards { get; init; } = System.Array.Empty<CardModel>();
}
public interface IWatcherProphecyListener
{
	Task OnProphecy(Player owner, ProphecyContext ctx);
}
public interface IProphecyCard
{
}
internal static class WatcherProphecy
{
	private static readonly FieldInfo? _nextMoveBackingField = typeof(MonsterModel).GetField("<NextMove>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

	public static async Task Trigger(Player owner, ProphecyContext ctx)
	{
		if (owner?.Creature == null)
		{
			return;
		}
		foreach (PowerModel power in owner.Creature.Powers.ToList())
		{
			if (power is IWatcherProphecyListener watcherProphecyListener)
			{
				try
				{
					await watcherProphecyListener.OnProphecy(owner, ctx);
				}
				catch (Exception ex)
				{
					Log.Error("[Watcher] Prophecy power " + power.GetType().Name + " failed: " + ex.Message);
				}
			}
		}
		foreach (RelicModel relic in owner.Relics.ToList())
		{
			if (relic is IWatcherProphecyListener watcherProphecyListener2)
			{
				try
				{
					await watcherProphecyListener2.OnProphecy(owner, ctx);
				}
				catch (Exception ex2)
				{
					Log.Error("[Watcher] Prophecy relic " + relic.GetType().Name + " failed: " + ex2.Message);
				}
			}
		}
	}

	public static MoveState? GetCurrentMove(Creature enemy)
	{
		try
		{
			return enemy?.Monster?.NextMove;
		}
		catch
		{
			return null;
		}
	}

	public static void RerollIntent(Creature enemy, IEnumerable<Creature> targets)
	{
		try
		{
			enemy.Monster?.RollMove(targets);
			NCombatRoom.Instance?.GetCreatureNode(enemy)?.RefreshIntents();
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] RerollIntent failed: " + ex.Message);
		}
	}

	public static void RefreshIntents(Creature enemy)
	{
		try
		{
			NCombatRoom.Instance?.GetCreatureNode(enemy)?.RefreshIntents();
		}
		catch
		{
		}
	}

	public static void StunEnemy(Creature enemy)
	{
		try
		{
			if (enemy?.Monster != null && !enemy.IsDead)
			{
				enemy.StunInternal((IReadOnlyList<Creature> _) => Task.CompletedTask, null);
			}
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] StunEnemy failed: " + ex.Message);
		}
	}

	public static async Task ApplyConfusion(Creature? target, Creature applier, CardModel source, int amount = 2)
	{
		try
		{
			if (target != null && !target.IsDead && amount > 0)
			{
				await WatcherPowerCmdCompat.Apply<ConfusionPower>(target, amount, applier, source);
			}
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] ApplyConfusion failed: " + ex.Message);
		}
	}

	public static void ForceStunEnemy(Creature enemy)
	{
		try
		{
			if (enemy?.Monster != null && !enemy.IsDead)
			{
				string text = enemy.Monster.NextMove?.Id;
				if (string.IsNullOrEmpty(text))
				{
					List<MonsterState> stateLog = enemy.Monster.MoveStateMachine.StateLog;
					text = ((stateLog.Count > 0) ? stateLog.Last().Id : null);
				}
				MoveState state = new MoveState("STUNNED", (IReadOnlyList<Creature> _) => Task.CompletedTask, new StunIntent())
				{
					FollowUpStateId = text,
					MustPerformOnceBeforeTransitioning = true
				};
				enemy.Monster.SetMoveImmediate(state, forceTransition: true);
				RefreshIntents(enemy);
			}
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] ForceStunEnemy failed: " + ex.Message);
		}
	}

	public static bool IsIntentFixed(Creature enemy)
	{
		try
		{
			return enemy != null && enemy.Monster?.NextMove?.CanTransitionAway == false;
		}
		catch
		{
			return false;
		}
	}

	public static bool RerollOrStun(Creature enemy, IEnumerable<Creature> targets)
	{
		if (enemy?.Monster == null)
		{
			return false;
		}
		if (IsIntentFixed(enemy))
		{
			ForceStunEnemy(enemy);
			return enemy.Monster.NextMove?.Id == "STUNNED";
		}
		string obj = enemy.Monster.NextMove?.Id;
		RerollIntent(enemy, targets);
		string text = enemy.Monster.NextMove?.Id;
		if (obj == text)
		{
			ForceStunEnemy(enemy);
			return enemy.Monster.NextMove?.Id == "STUNNED";
		}
		return true;
	}

	public static bool SwapIntents(Creature a, Creature b)
	{
		try
		{
			if (a?.Monster == null || b?.Monster == null)
			{
				return false;
			}
			if (a == b)
			{
				return false;
			}
			MoveState nextMove = a.Monster.NextMove;
			MoveState nextMove2 = b.Monster.NextMove;
			bool flag = !nextMove.CanTransitionAway;
			bool flag2 = !nextMove2.CanTransitionAway;
			if (flag && flag2)
			{
				ForceStunEnemy(a);
				ForceStunEnemy(b);
				return a.Monster.NextMove?.Id == "STUNNED" || b.Monster.NextMove?.Id == "STUNNED";
			}
			if (flag)
			{
				ForceStunEnemy(a);
				return a.Monster.NextMove?.Id == "STUNNED";
			}
			if (flag2)
			{
				ForceStunEnemy(b);
				return b.Monster.NextMove?.Id == "STUNNED";
			}
			return SwapNextMoveDisplayOnly(a, b, nextMove, nextMove2) && nextMove.Id != nextMove2.Id;
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] SwapIntents failed: " + ex.Message);
			return false;
		}
	}

	public static bool OverrideNextMove(Creature enemy, MoveState move)
	{
		try
		{
			if (enemy?.Monster == null || _nextMoveBackingField == null)
			{
				return false;
			}
			_nextMoveBackingField.SetValue(enemy.Monster, move);
			RefreshIntents(enemy);
			return true;
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] OverrideNextMove failed: " + ex.Message);
			return false;
		}
	}

	public static bool SwapNextMoveDisplayOnly(Creature a, Creature b, MoveState moveA, MoveState moveB)
	{
		bool num = OverrideNextMove(a, moveB);
		bool flag = OverrideNextMove(b, moveA);
		return num && flag;
	}
}
public sealed class KnowFatePower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	protected override bool IsVisibleInternal => false;
}
public abstract class WatcherV2ChoiceTokenBase : WatcherCard
{
	protected override bool IsPlayable => false;

	public override bool CanBeGeneratedInCombat => false;

	public override string PortraitPath => "res://images/packed/card_portraits/watcher/_placeholder.png";

	public override string BetaPortraitPath => "res://images/packed/card_portraits/watcher/_placeholder.png";

	protected WatcherV2ChoiceTokenBase()
		: base(-1, CardType.Skill, CardRarity.Token, TargetType.Self, shouldShowInCardLibrary: false)
	{
	}

	protected override Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return Task.CompletedTask;
	}
}
public sealed class EnlightenFatePower : PowerModel
{
	private int _pendingMantra;

	private bool _queuedThisTurn;

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		if (side != base.Owner.Side || _queuedThisTurn)
		{
			return Task.CompletedTask;
		}
		WatcherStatePower? power = base.Owner.GetPower<WatcherStatePower>();
		if (power != null && power.KnowFateConsumedThisTurn)
		{
			_queuedThisTurn = true;
			_pendingMantra = base.Amount;
		}
		return Task.CompletedTask;
	}

	public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
	{
		if (player == base.Owner.Player)
		{
			_queuedThisTurn = false;
			if (_pendingMantra > 0)
			{
				int pendingMantra = _pendingMantra;
				_pendingMantra = 0;
				Flash();
				await WatcherCombatHelper.GainMantra(player, pendingMantra, null);
			}
		}
	}
}
public sealed class BlessProphecyDamagePower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
	{
		if (dealer != base.Owner)
		{
			return 0m;
		}
		if (!props.IsPoweredAttack())
		{
			return 0m;
		}
		if (!(cardSource is IProphecyCard))
		{
			return 0m;
		}
		return base.Amount;
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (cardPlay.Card.Owner == base.Owner.Player && cardPlay.Card.Type == CardType.Attack && cardPlay.Card is IProphecyCard)
		{
			await PowerCmd.Remove(this);
		}
	}

	public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		if (side == base.Owner.Side)
		{
			await PowerCmd.Remove(this);
		}
	}
}
public sealed class GuardNextScryPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Single;
}
internal static class ProphetBridge
{
	public static bool IsGen2(CharacterModel? character)
	{
		return character?.GetType().Name == "WatcherV2";
	}

	public static CharacterModel? Gen2Character()
	{
		return ModelDb.AllCharacters.FirstOrDefault((CharacterModel c) => c.GetType().Name == "WatcherV2");
	}
}
internal static class WatcherV2AssetRedirect
{
	public static void Apply(ref string __result)
	{
		if (!string.IsNullOrEmpty(__result) && __result.Contains("watcher_v2"))
		{
			__result = __result.Replace("watcher_v2", "watcher");
		}
	}
}
public sealed class ConfusionPower : PowerModel
{
	private const string _damageIncrease = "DamageIncrease";

	public override PowerType Type => PowerType.Debuff;

	public override PowerStackType StackType => PowerStackType.Counter;

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("DamageIncrease", 1.5m) };

	public decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
	{
		if (target != base.Owner)
		{
			return 1m;
		}
		if (!props.IsPoweredAttack())
		{
			return 1m;
		}
		if (!(cardSource is IProphecyCard))
		{
			return 1m;
		}
		return base.DynamicVars["DamageIncrease"].BaseValue;
	}

	public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		if (side == CombatSide.Enemy)
		{
			await PowerCmd.TickDownDuration(this);
		}
	}
}
public sealed class ForecastedMovesPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Single;

	protected override bool IsVisibleInternal => false;

	public Queue<MoveState?> Queue { get; } = new Queue<MoveState>();

	internal async Task AfterSideTurnStartCompat(CombatSide side)
	{
		if (side != CombatSide.Player || base.Owner == null || base.Owner.IsDead)
		{
			return;
		}
		if (Queue.Count == 0)
		{
			await PowerCmd.Remove(this);
			return;
		}
		MoveState moveState = Queue.Dequeue();
		try
		{
			if (moveState == null)
			{
				WatcherProphecy.ForceStunEnemy(base.Owner);
			}
			else
			{
				base.Owner.Monster?.SetMoveImmediate(moveState, forceTransition: true);
				WatcherProphecy.RefreshIntents(base.Owner);
			}
		}
		catch
		{
		}
		if (Queue.Count == 0)
		{
			await PowerCmd.Remove(this);
		}
	}
}
public sealed class DeepThoughtSleepPower : PowerModel
{
	private bool _blockDepleted;

	private bool _triggered;

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Single;

	public int VulnerableAmount { get; set; } = 2;

	public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (_triggered)
		{
			return Task.CompletedTask;
		}
		if (target != base.Owner)
		{
			return Task.CompletedTask;
		}
		if (base.Owner.Block <= 0)
		{
			_blockDepleted = true;
		}
		return Task.CompletedTask;
	}

	internal async Task AfterSideTurnStartCompat(CombatSide side, object? combatState)
	{
		if (_triggered || side != CombatSide.Player || base.Owner == null || base.Owner.IsDead || base.Owner.Player == null)
		{
			return;
		}
		_triggered = true;
		Flash();
		if (!_blockDepleted)
		{
			await WatcherCombatHelper.EnterCalm(base.Owner.Player, null);
		}
		else
		{
			await WatcherCombatHelper.EnterWrath(base.Owner.Player, null);
			IReadOnlyList<Creature> enemies = WatcherHookCompat.GetEnemies(combatState);
			foreach (Creature item in enemies)
			{
				await WatcherPowerCmdCompat.Apply<VulnerablePower>(item, VulnerableAmount, base.Owner, null);
			}
		}
		await PowerCmd.Remove(this);
	}
}
internal static class WatcherSkeletonHelper
{
	private static bool _logged;

	public static void ApplySkeletonVariant(MegaSprite sprite)
	{
		string activeSkeletonDataPath = WatcherModSettings.ActiveSkeletonDataPath;
		if (!(activeSkeletonDataPath == "res://animations/characters/watcher/watcher_skel_data.tres"))
		{
			ApplySkeletonDataPath(sprite, activeSkeletonDataPath, "community");
		}
	}

	public static void ApplySkeletonDataPath(MegaSprite sprite, string path, string label = "custom")
	{
		try
		{
			Resource resource = ResourceLoader.Load<Resource>(path, null, ResourceLoader.CacheMode.Reuse);
			if (resource == null)
			{
				Log.Error("[Watcher] Failed to load skeleton data: " + path);
				return;
			}
			MegaSkeletonDataResource skeletonDataRes = new MegaSkeletonDataResource(resource);
			sprite.SetSkeletonDataRes(skeletonDataRes);
			if (!_logged)
			{
				_logged = true;
				GD.Print("[Watcher] Using " + label + " skeleton: " + path);
			}
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] Skeleton swap error: " + ex.Message);
		}
	}

	public static void ApplySkeletonVariant(Node spineNode)
	{
		if (!(spineNode.GetClass() != "SpineSprite"))
		{
			ApplySkeletonVariant(new MegaSprite(spineNode));
		}
	}

	public static bool ApplyCharSelectSkeleton(Node? spineNode, string path, string animName)
	{
		if (spineNode == null || spineNode.GetClass() != "SpineSprite")
		{
			return false;
		}
		Resource resource = ResourceLoader.Load<Resource>(path, null, ResourceLoader.CacheMode.Reuse);
		if (resource == null)
		{
			Log.Warn("[Watcher] prophet char-select skeleton not loadable (add-on PCK missing/stale?): " + path);
			return false;
		}
		try
		{
			MegaSprite megaSprite = new MegaSprite(spineNode);
			megaSprite.SetSkeletonDataRes(new MegaSkeletonDataResource(resource));
			WatcherSpineCompat.SetAnimation(megaSprite.GetAnimationState(), animName);
			return true;
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] prophet char-select swap error: " + ex.Message);
			return false;
		}
	}

	public static void BindEyeToBone(Node2D? visualsNode, string? boneName, Vector2 offset, float rotation, float scale)
	{
		if (visualsNode == null)
		{
			return;
		}
		Node nodeOrNull = visualsNode.GetNodeOrNull("EyeAnchor");
		if (nodeOrNull != null && !string.IsNullOrEmpty(boneName))
		{
			nodeOrNull.Set("bone_name", boneName);
		}
		Node2D nodeOrNull2 = visualsNode.GetNodeOrNull<Node2D>("EyeAnchor/EyeSprite");
		if (nodeOrNull2 == null)
		{
			return;
		}
		nodeOrNull2.Position = offset;
		nodeOrNull2.Rotation = rotation;
		nodeOrNull2.Scale = Vector2.One * scale;
		try
		{
			WatcherSpineCompat.SetAnimation(new MegaSprite(nodeOrNull2).GetAnimationState(), "None");
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] Eye rebind error: " + ex.Message);
		}
	}

	public static void HideEye(Node2D? visualsNode)
	{
		if (visualsNode != null)
		{
			if (visualsNode.GetNodeOrNull("EyeAnchor") is CanvasItem canvasItem)
			{
				canvasItem.Visible = false;
			}
			if (visualsNode.GetNodeOrNull("EyeAnchor/EyeSprite") is CanvasItem canvasItem2)
			{
				canvasItem2.Visible = false;
			}
		}
	}

	public static Node? EnsureEyeTop(Node2D? visualsNode)
	{
		if (visualsNode == null || WatcherModSettings.UseCommunitySkeleton)
		{
			return null;
		}
		Node nodeOrNull = visualsNode.GetNodeOrNull("EyeAnchor/EyeSprite");
		if (nodeOrNull == null)
		{
			return null;
		}
		try
		{
			WatcherSpineCompat.SetAnimation(new MegaSprite(nodeOrNull).GetAnimationState(), "None");
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] Eye top skeleton error: " + ex.Message);
		}
		return nodeOrNull;
	}

	public static void UpdateEyeTop(Node2D? visualsNode, string animationName)
	{
		if (visualsNode == null || WatcherModSettings.UseCommunitySkeleton)
		{
			return;
		}
		Node nodeOrNull = visualsNode.GetNodeOrNull("EyeAnchor/EyeSprite");
		if (nodeOrNull == null)
		{
			return;
		}
		try
		{
			WatcherSpineCompat.SetAnimation(new MegaSprite(nodeOrNull).GetAnimationState(), animationName);
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] Eye animation error: " + ex.Message);
		}
	}
}
internal static class WatcherSpineCompat
{
	private static readonly MethodInfo? SetAnimationWithTrackMethod = FindSetAnimationMethod(typeof(string), typeof(bool), typeof(int));

	private static readonly MethodInfo? SetAnimationMethod = FindSetAnimationMethod(typeof(string), typeof(bool));

	public static void SetAnimation(MegaAnimationState? state, string animationName, bool loop = true, int trackId = 0)
	{
		if (state == null)
		{
			return;
		}
		MethodInfo methodInfo = SetAnimationWithTrackMethod ?? SetAnimationMethod;
		if (methodInfo == null)
		{
			throw new MissingMethodException(typeof(MegaAnimationState).FullName, "SetAnimation");
		}
		object[] parameters = ((methodInfo.GetParameters().Length != 3) ? new object[2] { animationName, loop } : new object[3] { animationName, loop, trackId });
		try
		{
			methodInfo.Invoke(state, parameters);
		}
		catch (TargetInvocationException ex) when (ex.InnerException != null)
		{
			ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
			throw;
		}
	}

	private static MethodInfo? FindSetAnimationMethod(params Type[] parameterTypes)
	{
		return typeof(MegaAnimationState).GetMethod("SetAnimation", BindingFlags.Instance | BindingFlags.Public, null, parameterTypes, null);
	}
}
internal static class WatcherTimelineLayer
{
	internal const string LayerName = "WatcherTimelineLayer";

	private const string StarTexturePath = "res://images/watcher/watcher_story_star.png";

	private static Texture2D? _starTex;

	private static readonly System.Collections.Generic.Dictionary<bool, Font?> _fontCache = new System.Collections.Generic.Dictionary<bool, Font>();

	public static void Inject(Node? bgNode)
	{
		try
		{
			if (!(bgNode is Control control))
			{
				return;
			}
			Node nodeOrNull = control.GetNodeOrNull("WatcherTimelineLayer");
			if (nodeOrNull != null && GodotObject.IsInstanceValid(nodeOrNull))
			{
				nodeOrNull.QueueFree();
			}
			List<EpochModel> revealedEpochs = GetRevealedEpochs();
			if (revealedEpochs.Count == 0)
			{
				return;
			}
			Control control2 = new Control
			{
				Name = "WatcherTimelineLayer",
				MouseFilter = Control.MouseFilterEnum.Ignore,
				AnchorRight = 1f,
				AnchorBottom = 1f,
				OffsetLeft = 0f,
				OffsetTop = 0f,
				OffsetRight = 0f,
				OffsetBottom = 0f
			};
			control.AddChild(control2, forceReadableName: false, Node.InternalMode.Disabled);
			WatcherStoryPopup popup = new WatcherStoryPopup(control2);
			WatcherStarfield watcherStarfield = new WatcherStarfield(control2);
			foreach (EpochModel item in revealedEpochs)
			{
				EpochModel captured = item;
				watcherStarfield.AddStar(captured, delegate
				{
					popup.ShowEpoch(captured);
				});
			}
			Log.Info($"[Watcher] timeline layer: {revealedEpochs.Count} revealed epoch point(s)");
		}
		catch (Exception value)
		{
			Log.Error($"[Watcher] timeline layer inject failed: {value}");
		}
	}

	private static List<EpochModel> GetRevealedEpochs()
	{
		List<EpochModel> list = new List<EpochModel>();
		try
		{
			ProgressState progressState = SaveManager.Instance?.Progress;
			if (progressState == null)
			{
				return list;
			}
			foreach (SerializableEpoch epoch in progressState.Epochs)
			{
				if (epoch.State < EpochState.Revealed)
				{
					continue;
				}
				try
				{
					EpochModel epochModel = EpochModel.Get(epoch.Id);
					if (epochModel != null)
					{
						list.Add(epochModel);
					}
				}
				catch
				{
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] reading revealed epochs failed: " + ex.Message);
		}
		return list;
	}

	internal static Texture2D? StarTexture()
	{
		if (_starTex != null && GodotObject.IsInstanceValid(_starTex))
		{
			return _starTex;
		}
		_starTex = WatcherTextureHelper.LoadTexture("res://images/watcher/watcher_story_star.png");
		return _starTex;
	}

	internal static Font? GetFont(bool bold)
	{
		if (_fontCache.TryGetValue(bold, out Font value) && value != null && GodotObject.IsInstanceValid(value))
		{
			return value;
		}
		string locale = TranslationServer.GetLocale();
		string text = ((locale.Length >= 2) ? locale.Substring(0, 2).ToLowerInvariant() : "en") switch
		{
			"ja" => bold ? "res://themes/fonts/jpn/noto_sans_cjkjp_bold_shared.tres" : "res://themes/fonts/jpn/noto_sans_cjkjp_medium_shared.tres", 
			"ko" => "res://themes/fonts/kor/gyeonggi_cheonnyeon_batang_bold_shared.tres", 
			"ru" => bold ? "res://themes/fonts/rus/fira_sans_extra_condensed_bold_shared.tres" : "res://themes/fonts/rus/fira_sans_extra_condensed_regular_shared.tres", 
			"th" => "res://themes/fonts/tha/cs_chat_thai_ui_shared.tres", 
			_ => bold ? "res://themes/fonts/zhs/source_han_serif_sc_bold_shared.tres" : "res://themes/fonts/zhs/source_han_serif_sc_medium_shared.tres", 
		};
		Font font = null;
		try
		{
			if (ResourceLoader.Exists(text))
			{
				font = ResourceLoader.Load<Font>(text, null, ResourceLoader.CacheMode.Reuse);
			}
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] timeline font load failed (" + text + "): " + ex.Message);
		}
		if (font == null)
		{
			string path = (bold ? "res://fonts/zhs/SourceHanSerifSC-Bold.otf" : "res://fonts/zhs/SourceHanSerifSC-Medium.otf");
			try
			{
				if (ResourceLoader.Exists(path))
				{
					font = ResourceLoader.Load<Font>(path, null, ResourceLoader.CacheMode.Reuse);
				}
			}
			catch
			{
			}
		}
		_fontCache[bold] = font;
		return font;
	}

	internal static Texture2D? EpochPortrait(EpochModel epoch)
	{
		try
		{
			return epoch.Portrait;
		}
		catch
		{
			return null;
		}
	}

	internal static uint StableHash(string s)
	{
		uint num = 2166136261u;
		foreach (char c in s)
		{
			num ^= c;
			num *= 16777619;
		}
		return num;
	}
}
internal sealed class WatcherStoryStar
{
	public TextureButton Button;

	public float Phase;

	public float Speed;

	public float BaseAlpha;

	public float BaseScale;

	public float HoverScale;

	public bool Hovered;

	public float CurScale;
}
internal sealed class WatcherStarfield
{
	private readonly Control _root;

	private readonly List<WatcherStoryStar> _stars = new List<WatcherStoryStar>();

	private bool _disposed;

	private int _index;

	public bool IsAlive
	{
		get
		{
			if (!_disposed && GodotObject.IsInstanceValid(_root))
			{
				return _root.IsInsideTree();
			}
			return false;
		}
	}

	public WatcherStarfield(Control parent)
	{
		_root = parent;
		_root.TreeExiting += OnTreeExiting;
		WatcherStarfieldManager.Register(this);
	}

	public void AddStar(EpochModel epoch, Action onClick)
	{
		Texture2D textureNormal = WatcherTimelineLayer.StarTexture();
		Random random = new Random((int)WatcherTimelineLayer.StableHash(epoch.Id));
		float x = (float)_index * 2.3999631f + (float)(random.NextDouble() - 0.5) * 0.9f;
		float num = 0.33f + (float)random.NextDouble() * 0.22f;
		float num2 = Math.Clamp(0.57f + MathF.Cos(x) * num * 0.82f, 0.04f, 0.96f);
		float num3 = Math.Clamp(0.48f + MathF.Sin(x) * num * 1.05f, 0.05f, 0.92f);
		float num4 = 26f + (float)random.NextDouble() * 24f;
		float phase = (float)random.NextDouble() * ((float)Math.PI * 2f);
		float speed = 1.4f + (float)random.NextDouble() * 1.6f;
		_index++;
		TextureButton textureButton = new TextureButton
		{
			Name = "StoryStar_" + epoch.Id,
			TextureNormal = textureNormal,
			IgnoreTextureSize = true,
			StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
			MouseFilter = Control.MouseFilterEnum.Stop,
			TooltipText = (epoch.StoryTitle ?? epoch.Title.GetFormattedText()),
			AnchorLeft = num2,
			AnchorRight = num2,
			AnchorTop = num3,
			AnchorBottom = num3,
			OffsetLeft = (0f - num4) * 0.5f,
			OffsetRight = num4 * 0.5f,
			OffsetTop = (0f - num4) * 0.5f,
			OffsetBottom = num4 * 0.5f,
			PivotOffset = new Vector2(num4 * 0.5f, num4 * 0.5f)
		};
		WatcherStoryStar star = new WatcherStoryStar
		{
			Button = textureButton,
			Phase = phase,
			Speed = speed,
			BaseAlpha = 0.55f + (float)random.NextDouble() * 0.25f,
			BaseScale = 1f,
			HoverScale = 1.6f,
			CurScale = 1f
		};
		textureButton.Pressed += delegate
		{
			try
			{
				onClick();
			}
			catch (Exception ex)
			{
				Log.Error("[Watcher] star click failed: " + ex.Message);
			}
		};
		textureButton.MouseEntered += delegate
		{
			star.Hovered = true;
		};
		textureButton.MouseExited += delegate
		{
			star.Hovered = false;
		};
		_root.AddChild(textureButton, forceReadableName: false, Node.InternalMode.Disabled);
		_stars.Add(star);
	}

	private void OnTreeExiting()
	{
		Dispose();
	}

	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
			WatcherStarfieldManager.Unregister(this);
		}
	}

	public void Tick(float delta, float time)
	{
		if (!IsAlive)
		{
			Dispose();
			return;
		}
		foreach (WatcherStoryStar star in _stars)
		{
			if (GodotObject.IsInstanceValid(star.Button))
			{
				float num = 0.55f + 0.45f * MathF.Sin(time * star.Speed + star.Phase);
				float value = (star.Hovered ? 1f : (star.BaseAlpha * num));
				Color modulate = star.Button.Modulate;
				modulate.A = Math.Clamp(value, 0f, 1f);
				star.Button.Modulate = modulate;
				float num2 = (star.Hovered ? star.HoverScale : star.BaseScale);
				star.CurScale += (num2 - star.CurScale) * Math.Min(1f, delta * 12f);
				star.Button.Scale = new Vector2(star.CurScale, star.CurScale);
			}
		}
	}
}
internal static class WatcherStarfieldManager
{
	private static readonly List<WatcherStarfield> _fields = new List<WatcherStarfield>();

	private static bool _hooked;

	private static double _lastTime;

	private static float _time;

	public static void Register(WatcherStarfield field)
	{
		EnsureHooked();
		_fields.Add(field);
	}

	public static void Unregister(WatcherStarfield field)
	{
		_fields.Remove(field);
	}

	private static void EnsureHooked()
	{
		if (!_hooked && Engine.GetMainLoop() is SceneTree sceneTree)
		{
			sceneTree.ProcessFrame += Tick;
			_hooked = true;
			_lastTime = (double)Time.GetTicksMsec() / 1000.0;
		}
	}

	private static void Tick()
	{
		double num = (double)Time.GetTicksMsec() / 1000.0;
		float num2 = (float)Math.Max(0.0, num - _lastTime);
		_lastTime = num;
		if (num2 > 0.1f)
		{
			num2 = 0.1f;
		}
		_time += num2;
		for (int num3 = _fields.Count - 1; num3 >= 0; num3--)
		{
			WatcherStarfield watcherStarfield = _fields[num3];
			if (!watcherStarfield.IsAlive)
			{
				watcherStarfield.Dispose();
				_fields.RemoveAt(num3);
			}
			else
			{
				try
				{
					watcherStarfield.Tick(num2, _time);
				}
				catch (Exception value)
				{
					Log.Error($"[Watcher] starfield tick failed: {value}");
				}
			}
		}
	}
}
internal sealed class WatcherStoryPopup
{
	private const string OpenSfx = "event:/sfx/ui/timeline/ui_timeline_open_epoch";

	private readonly Control _root;

	private readonly TextureRect _portrait;

	private readonly Label _storyTitle;

	private readonly Label _chapter;

	private readonly Label _era;

	private readonly MegaRichTextLabel _body;

	public WatcherStoryPopup(Node parent)
	{
		Font font = WatcherTimelineLayer.GetFont(bold: true);
		Font font2 = WatcherTimelineLayer.GetFont(bold: false);
		CanvasLayer canvasLayer = new CanvasLayer
		{
			Name = "WatcherStoryPopupLayer",
			Layer = 128
		};
		parent.AddChild(canvasLayer, forceReadableName: false, Node.InternalMode.Disabled);
		_root = new Control
		{
			Name = "StoryPopup",
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		_root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize);
		canvasLayer.AddChild(_root, forceReadableName: false, Node.InternalMode.Disabled);
		ColorRect colorRect = new ColorRect
		{
			Name = "Bg",
			Color = new Color(0.06f, 0.04f, 0.12f, 0.965f),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		colorRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize);
		_root.AddChild(colorRect, forceReadableName: false, Node.InternalMode.Disabled);
		MarginContainer marginContainer = new MarginContainer();
		marginContainer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize);
		marginContainer.AddThemeConstantOverride("margin_left", 200);
		marginContainer.AddThemeConstantOverride("margin_right", 200);
		marginContainer.AddThemeConstantOverride("margin_top", 60);
		marginContainer.AddThemeConstantOverride("margin_bottom", 120);
		_root.AddChild(marginContainer, forceReadableName: false, Node.InternalMode.Disabled);
		VBoxContainer vBoxContainer = new VBoxContainer();
		vBoxContainer.AddThemeConstantOverride("separation", 10);
		marginContainer.AddChild(vBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		_storyTitle = MakeLabel(font, 46, new Color(0.96f, 0.9f, 1f));
		vBoxContainer.AddChild(_storyTitle, forceReadableName: false, Node.InternalMode.Disabled);
		_chapter = MakeLabel(font2, 26, new Color(0.8f, 0.72f, 0.96f));
		vBoxContainer.AddChild(_chapter, forceReadableName: false, Node.InternalMode.Disabled);
		_era = MakeLabel(font2, 20, new Color(0.62f, 0.56f, 0.8f));
		vBoxContainer.AddChild(_era, forceReadableName: false, Node.InternalMode.Disabled);
		_portrait = new TextureRect
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			CustomMinimumSize = new Vector2(0f, 380f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		vBoxContainer.AddChild(_portrait, forceReadableName: false, Node.InternalMode.Disabled);
		_body = new MegaRichTextLabel
		{
			BbcodeEnabled = true,
			AutoSizeEnabled = false,
			FitContent = false,
			ScrollActive = true,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
		};
		if (font2 != null)
		{
			_body.AddThemeFontOverride("normal_font", font2);
			_body.AddThemeFontOverride("italics_font", font2);
			_body.AddThemeFontOverride("mono_font", font2);
		}
		if (font != null)
		{
			_body.AddThemeFontOverride("bold_font", font);
			_body.AddThemeFontOverride("bold_italics_font", font);
		}
		_body.AddThemeFontSizeOverride("normal_font_size", 26);
		_body.AddThemeColorOverride("default_color", new Color(0.92f, 0.9f, 0.98f));
		vBoxContainer.AddChild(_body, forceReadableName: false, Node.InternalMode.Disabled);
		AddBackButton();
	}

	private void AddBackButton()
	{
		try
		{
			NBackButton nBackButton = PreloadManager.Cache.GetScene("res://scenes/ui/back_button.tscn").Instantiate<NBackButton>(PackedScene.GenEditState.Disabled);
			nBackButton.Name = "WatcherStoryBackButton";
			nBackButton.Connect(NClickableControl.SignalName.Released, Callable.From<NButton>(delegate
			{
				Close();
			}));
			_root.AddChild(nBackButton, forceReadableName: false, Node.InternalMode.Disabled);
			nBackButton.Enable();
			return;
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] NBackButton load failed, using fallback: " + ex.Message);
		}
		Button button = new Button
		{
			Text = "‹ Back",
			MouseFilter = Control.MouseFilterEnum.Stop,
			AnchorTop = 1f,
			AnchorBottom = 1f,
			OffsetLeft = 60f,
			OffsetTop = -110f,
			OffsetRight = 240f,
			OffsetBottom = -54f
		};
		button.Pressed += Close;
		_root.AddChild(button, forceReadableName: false, Node.InternalMode.Disabled);
	}

	private static Label MakeLabel(Font? font, int size, Color color)
	{
		Label label = new Label
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		if (font != null)
		{
			label.AddThemeFontOverride("font", font);
		}
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	public void ShowEpoch(EpochModel epoch)
	{
		_portrait.Texture = WatcherTimelineLayer.EpochPortrait(epoch);
		string text = epoch.StoryTitle ?? string.Empty;
		bool flag = !string.IsNullOrEmpty(text);
		_storyTitle.Text = (flag ? text : epoch.Title.GetFormattedText());
		_chapter.Text = (flag ? epoch.Title.GetFormattedText() : string.Empty);
		_chapter.Visible = flag;
		string text2 = StringHelper.Slugify(epoch.Era.ToString());
		string formattedText = new LocString("eras", text2 + ".name").GetFormattedText();
		string formattedText2 = new LocString("eras", text2 + ".year").GetFormattedText();
		bool flag2 = !string.IsNullOrWhiteSpace(formattedText) || !string.IsNullOrWhiteSpace(formattedText2);
		_era.Text = (flag2 ? (formattedText + " · " + formattedText2) : string.Empty);
		_era.Visible = flag2;
		_body.VisibleRatio = 1f;
		_body.ParseBbcode(epoch.Description);
		_root.Visible = true;
		try
		{
			SfxCmd.Play("event:/sfx/ui/timeline/ui_timeline_open_epoch");
		}
		catch
		{
		}
	}

	private void Close()
	{
		if (GodotObject.IsInstanceValid(_root))
		{
			_root.Visible = false;
		}
	}
}
public sealed class Wrath : PowerModel
{
	private Node2D? _vfx;

	private Node? _borderVfx;

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Single;

	protected override bool IsVisibleInternal => false;

	public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
	{
		_vfx = StanceVfxHelper.SpawnOnCreature(base.Owner, StanceVfxHelper.CreateWrathVfx());
		_borderVfx = StanceVfxHelper.SpawnWrathBorder(base.Owner);
		WatcherAudioHelper.PlayOneShot("res://audio/watcher/wrath.ogg");
		await Task.CompletedTask;
	}

	public override async Task AfterRemoved(Creature oldOwner)
	{
		StanceVfxHelper.Remove(ref _vfx);
		StanceVfxHelper.RemoveBorder(ref _borderVfx);
		await Task.CompletedTask;
	}

	public decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
	{
		bool flag = props.HasFlag(ValueProp.Move) && !props.HasFlag(ValueProp.Unpowered);
		if (dealer == base.Owner && flag)
		{
			return 2m;
		}
		if (target == base.Owner && flag)
		{
			return 2m;
		}
		return 1m;
	}
}
public sealed class Calm : PowerModel
{
	private Node2D? _vfx;

	private Node? _borderVfx;

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Single;

	protected override bool IsVisibleInternal => false;

	public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
	{
		_vfx = StanceVfxHelper.SpawnOnCreature(base.Owner, StanceVfxHelper.CreateCalmVfx());
		_borderVfx = StanceVfxHelper.SpawnCalmBorder(base.Owner);
		WatcherAudioHelper.PlayOneShot("res://audio/watcher/calm.ogg");
		await Task.CompletedTask;
	}

	public override async Task AfterRemoved(Creature oldOwner)
	{
		StanceVfxHelper.Remove(ref _vfx);
		StanceVfxHelper.RemoveBorder(ref _borderVfx);
		if (CombatManager.Instance.IsInProgress && oldOwner.Player != null)
		{
			await PlayerCmd.GainEnergy(2m, oldOwner.Player);
		}
	}
}
public sealed class Foreseen : PowerModel
{
	private Node2D? _vfx;

	private Node? _borderVfx;

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Single;

	protected override bool IsVisibleInternal => false;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		HoverTipFactory.FromPower<KnowFatePower>(null),
		HoverTipFactory.FromPower<EnlightenFatePower>(null)
	};

	public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
	{
		_vfx = StanceVfxHelper.SpawnOnCreature(base.Owner, StanceVfxHelper.CreateForeseenVfx());
		_borderVfx = StanceVfxHelper.SpawnForeseenBorder(base.Owner);
		WatcherAudioHelper.PlayOneShot("res://audio/watcher/calm.ogg");
		await Task.CompletedTask;
	}

	public override async Task AfterRemoved(Creature oldOwner)
	{
		StanceVfxHelper.Remove(ref _vfx);
		StanceVfxHelper.RemoveBorder(ref _borderVfx);
		await Task.CompletedTask;
	}

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player == base.Owner.Player)
		{
			await WatcherPowerCmdCompat.Apply<KnowFatePower>(base.Owner, 2m, base.Owner, null);
		}
	}
}
public sealed class Divinity : PowerModel
{
	private Node2D? _vfx;

	private Node? _borderVfx;

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Single;

	protected override bool IsVisibleInternal => false;

	public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
	{
		WatcherAttackVfxHelper.PlayDivinityStanceChange(base.Owner);
		_vfx = StanceVfxHelper.SpawnOnCreature(base.Owner, StanceVfxHelper.CreateDivinityVfx());
		WatcherAttackVfxHelper.StartDivinityEye(base.Owner);
		_borderVfx = StanceVfxHelper.SpawnDivinityBorder(base.Owner);
		WatcherAudioHelper.PlayOneShot("res://audio/watcher/divinity.ogg");
		if (base.Owner.Player != null)
		{
			await PlayerCmd.GainEnergy(3m, base.Owner.Player);
		}
	}

	public override async Task AfterRemoved(Creature oldOwner)
	{
		StanceVfxHelper.Remove(ref _vfx);
		WatcherAttackVfxHelper.StopDivinityEye(oldOwner);
		StanceVfxHelper.RemoveBorder(ref _borderVfx);
		await Task.CompletedTask;
	}

	public decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
	{
		bool flag = props.HasFlag(ValueProp.Move) && !props.HasFlag(ValueProp.Unpowered);
		if (dealer == base.Owner && flag)
		{
			return 3m;
		}
		return 1m;
	}

	public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		if (side == base.Owner.Side && base.Owner.Player != null)
		{
			await WatcherCombatHelper.ExitStance(base.Owner.Player);
		}
	}
}
public sealed class Mantra : PowerModel
{
	private bool _isResolving;

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	internal async Task AfterPowerAmountChangedCompat(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (_isResolving || power != this || base.Owner.Player == null || base.Amount < 10)
		{
			return;
		}
		_isResolving = true;
		try
		{
			WatcherAudioHelper.PlayOneShot("res://audio/watcher/mantra.ogg");
			await WatcherPowerCmdCompat.ModifyAmount(this, -10m, applier, cardSource, silent: true);
			await WatcherCombatHelper.EnterDivinity(base.Owner.Player, cardSource);
		}
		finally
		{
			_isResolving = false;
		}
	}
}
public sealed class DevotionPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player == base.Owner.Player)
		{
			await WatcherCombatHelper.GainMantra(player, base.Amount, null);
		}
	}
}
internal static class StanceVfxHelper
{
	private const string VignetteShader = "\r\nshader_type canvas_item;\r\n\r\nuniform vec4 border_color : source_color = vec4(1.0, 0.2, 0.1, 0.5);\r\nuniform vec4 border_color_2 : source_color = vec4(1.0, 0.5, 0.1, 0.3);\r\nuniform float border_width : hint_range(0.01, 0.5) = 0.15;\r\nuniform float noise_scale : hint_range(1.0, 30.0) = 8.0;\r\nuniform float noise_speed : hint_range(0.0, 5.0) = 1.5;\r\nuniform float noise_intensity : hint_range(0.0, 1.0) = 0.3;\r\nuniform vec2 noise_direction = vec2(0.0, -1.0);\r\nuniform float pulse_alpha : hint_range(0.0, 1.0) = 1.0;\r\n\r\nfloat hash(vec2 p) {\r\n    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);\r\n}\r\n\r\nfloat value_noise(vec2 p) {\r\n    vec2 i = floor(p);\r\n    vec2 f = fract(p);\r\n    f = f * f * (3.0 - 2.0 * f);\r\n    float a = hash(i);\r\n    float b = hash(i + vec2(1.0, 0.0));\r\n    float c = hash(i + vec2(0.0, 1.0));\r\n    float d = hash(i + vec2(1.0, 1.0));\r\n    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);\r\n}\r\n\r\nfloat fbm(vec2 p) {\r\n    float v = 0.0;\r\n    float a = 0.5;\r\n    for (int i = 0; i < 3; i++) {\r\n        v += a * value_noise(p);\r\n        p *= 2.0;\r\n        a *= 0.5;\r\n    }\r\n    return v;\r\n}\r\n\r\nvoid fragment() {\r\n    vec2 uv = UV;\r\n    float dx = min(uv.x, 1.0 - uv.x);\r\n    float dy = min(uv.y, 1.0 - uv.y);\r\n    float d = min(dx, dy);\r\n\r\n    // Animated fractal noise for organic border\r\n    vec2 noise_uv = uv * noise_scale + noise_direction * TIME * noise_speed;\r\n    float n1 = fbm(noise_uv);\r\n    float n2 = fbm(noise_uv * 0.7 - vec2(TIME * noise_speed * 0.4, 0.0));\r\n    float n = (n1 + n2) * 0.5;\r\n\r\n    // Distort edge distance with noise for fiery/watery look\r\n    float distorted_d = d + (n - 0.5) * noise_intensity * border_width;\r\n\r\n    float vignette = 1.0 - smoothstep(0.0, border_width, distorted_d);\r\n    // Softer inner glow layer\r\n    float inner_glow = 1.0 - smoothstep(0.0, border_width * 1.8, distorted_d);\r\n\r\n    // Two-tone color blending driven by noise\r\n    vec3 col = mix(border_color.rgb, border_color_2.rgb, n);\r\n    float base_alpha = mix(border_color.a, border_color_2.a, n * 0.5);\r\n\r\n    // Combine sharp border + soft inner glow\r\n    float alpha = max(vignette * base_alpha, inner_glow * base_alpha * 0.3);\r\n    COLOR = vec4(col, alpha * pulse_alpha);\r\n}\r\n";

	private static Texture2D? _exhaustTexture;

	private static Texture2D? _glowSparkTexture;

	private static Texture2D? _calmOrbTexture;

	public static Node2D? SpawnOnCreature(Creature owner, Node2D vfx)
	{
		NCreature nCreature = NCombatRoom.Instance?.GetCreatureNode(owner);
		if (nCreature == null)
		{
			vfx.QueueFree();
			return null;
		}
		nCreature.Visuals.AddChild(vfx, forceReadableName: false, Node.InternalMode.Disabled);
		return vfx;
	}

	public static void Remove(ref Node2D? vfx)
	{
		if (vfx != null && GodotObject.IsInstanceValid(vfx))
		{
			vfx.QueueFree();
		}
		vfx = null;
	}

	public static Node? SpawnWrathBorder(Creature owner)
	{
		return SpawnBorder(owner, new Color(0.8f, 0f, 0.1f, 0.42f), new Color(1f, 0.15f, 0.05f, 0.3f), 0.18f, 12f, 2.5f, 0.55f, new Vector2(0.3f, -1f));
	}

	public static Node? SpawnCalmBorder(Creature owner)
	{
		return SpawnBorder(owner, new Color(0.2f, 0.6f, 1f, 0.27f), new Color(0.5f, 0.7f, 1f, 0.15f), 0.13f, 6f, 0.5f, 0.35f, new Vector2(0.8f, 0.3f));
	}

	public static Node? SpawnDivinityBorder(Creature owner)
	{
		return SpawnBorder(owner, new Color(0.85f, 0.15f, 0.85f, 0.52f), new Color(1f, 0.6f, 1f, 0.36f), 0.25f, 8f, 1.2f, 0.4f, new Vector2(0f, -0.5f));
	}

	public static Node? SpawnForeseenBorder(Creature owner)
	{
		return SpawnBorder(owner, new Color(0.15f, 0.85f, 0.75f, 0.34f), new Color(0.55f, 0.9f, 1f, 0.22f), 0.15f, 7f, 0.8f, 0.35f, new Vector2(-0.4f, -0.7f));
	}

	private static Node? SpawnBorder(Creature owner, Color borderColor, Color borderColor2, float borderWidth, float noiseScale, float noiseSpeed, float noiseIntensity, Vector2 noiseDirection)
	{
		if (NCombatRoom.Instance == null || IsMultiplayerCombat(owner))
		{
			return null;
		}
		CanvasLayer canvasLayer = new CanvasLayer();
		canvasLayer.Name = "StanceBorderVfx";
		canvasLayer.Layer = 2;
		ColorRect colorRect = new ColorRect();
		colorRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize);
		colorRect.MouseFilter = Control.MouseFilterEnum.Ignore;
		Shader shader = new Shader();
		shader.Code = "\r\nshader_type canvas_item;\r\n\r\nuniform vec4 border_color : source_color = vec4(1.0, 0.2, 0.1, 0.5);\r\nuniform vec4 border_color_2 : source_color = vec4(1.0, 0.5, 0.1, 0.3);\r\nuniform float border_width : hint_range(0.01, 0.5) = 0.15;\r\nuniform float noise_scale : hint_range(1.0, 30.0) = 8.0;\r\nuniform float noise_speed : hint_range(0.0, 5.0) = 1.5;\r\nuniform float noise_intensity : hint_range(0.0, 1.0) = 0.3;\r\nuniform vec2 noise_direction = vec2(0.0, -1.0);\r\nuniform float pulse_alpha : hint_range(0.0, 1.0) = 1.0;\r\n\r\nfloat hash(vec2 p) {\r\n    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);\r\n}\r\n\r\nfloat value_noise(vec2 p) {\r\n    vec2 i = floor(p);\r\n    vec2 f = fract(p);\r\n    f = f * f * (3.0 - 2.0 * f);\r\n    float a = hash(i);\r\n    float b = hash(i + vec2(1.0, 0.0));\r\n    float c = hash(i + vec2(0.0, 1.0));\r\n    float d = hash(i + vec2(1.0, 1.0));\r\n    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);\r\n}\r\n\r\nfloat fbm(vec2 p) {\r\n    float v = 0.0;\r\n    float a = 0.5;\r\n    for (int i = 0; i < 3; i++) {\r\n        v += a * value_noise(p);\r\n        p *= 2.0;\r\n        a *= 0.5;\r\n    }\r\n    return v;\r\n}\r\n\r\nvoid fragment() {\r\n    vec2 uv = UV;\r\n    float dx = min(uv.x, 1.0 - uv.x);\r\n    float dy = min(uv.y, 1.0 - uv.y);\r\n    float d = min(dx, dy);\r\n\r\n    // Animated fractal noise for organic border\r\n    vec2 noise_uv = uv * noise_scale + noise_direction * TIME * noise_speed;\r\n    float n1 = fbm(noise_uv);\r\n    float n2 = fbm(noise_uv * 0.7 - vec2(TIME * noise_speed * 0.4, 0.0));\r\n    float n = (n1 + n2) * 0.5;\r\n\r\n    // Distort edge distance with noise for fiery/watery look\r\n    float distorted_d = d + (n - 0.5) * noise_intensity * border_width;\r\n\r\n    float vignette = 1.0 - smoothstep(0.0, border_width, distorted_d);\r\n    // Softer inner glow layer\r\n    float inner_glow = 1.0 - smoothstep(0.0, border_width * 1.8, distorted_d);\r\n\r\n    // Two-tone color blending driven by noise\r\n    vec3 col = mix(border_color.rgb, border_color_2.rgb, n);\r\n    float base_alpha = mix(border_color.a, border_color_2.a, n * 0.5);\r\n\r\n    // Combine sharp border + soft inner glow\r\n    float alpha = max(vignette * base_alpha, inner_glow * base_alpha * 0.3);\r\n    COLOR = vec4(col, alpha * pulse_alpha);\r\n}\r\n";
		ShaderMaterial shaderMaterial = new ShaderMaterial();
		shaderMaterial.Shader = shader;
		shaderMaterial.SetShaderParameter("border_color", borderColor);
		shaderMaterial.SetShaderParameter("border_color_2", borderColor2);
		shaderMaterial.SetShaderParameter("border_width", borderWidth);
		shaderMaterial.SetShaderParameter("noise_scale", noiseScale);
		shaderMaterial.SetShaderParameter("noise_speed", noiseSpeed);
		shaderMaterial.SetShaderParameter("noise_intensity", noiseIntensity);
		shaderMaterial.SetShaderParameter("noise_direction", noiseDirection);
		shaderMaterial.SetShaderParameter("pulse_alpha", 1f);
		colorRect.Material = shaderMaterial;
		canvasLayer.AddChild(colorRect, forceReadableName: false, Node.InternalMode.Disabled);
		NCombatRoom.Instance.AddChild(canvasLayer, forceReadableName: false, Node.InternalMode.Disabled);
		colorRect.Modulate = new Color(1f, 1f, 1f, 0f);
		Tween tween = colorRect.CreateTween();
		tween.TweenProperty(colorRect, "modulate:a", 1f, 0.6499999761581421).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
		tween.TweenInterval(0.3499999940395355);
		tween.TweenProperty(colorRect, "modulate:a", 0f, 2.4000000953674316).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
		tween.TweenCallback(Callable.From(delegate
		{
			if (GodotObject.IsInstanceValid(canvasLayer))
			{
				canvasLayer.QueueFree();
			}
		}));
		return canvasLayer;
	}

	private static bool IsMultiplayerCombat(Creature owner)
	{
		CombatState? combatState = WatcherCreatureCompat.GetCombatState(owner);
		if (combatState == null)
		{
			return false;
		}
		return combatState.PlayerCreatures.Count > 1;
	}

	public static void RemoveBorder(ref Node? borderVfx)
	{
		Node node = borderVfx;
		borderVfx = null;
		if (node == null || !GodotObject.IsInstanceValid(node))
		{
			return;
		}
		if (node is CanvasLayer canvasLayer && canvasLayer.GetChildCount() > 0 && canvasLayer.GetChild(0) is Control control)
		{
			Tween tween = control.CreateTween();
			tween.TweenProperty(control, "modulate:a", 0f, 0.6000000238418579).SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
			tween.TweenCallback(Callable.From(delegate
			{
				if (GodotObject.IsInstanceValid(node))
				{
					node.QueueFree();
				}
			}));
		}
		else
		{
			node.QueueFree();
		}
	}

	public static Node2D CreateCalmVfx()
	{
		Node2D obj = new Node2D
		{
			Name = "CalmVfx"
		};
		CpuParticles2D cpuParticles2D = CreateFogParticles(8, 2.0, new Vector2(30f, 40f), 2f, 2.7f, new Color(0.5f, 0.65f, 1f), 0.15f, 40f);
		cpuParticles2D.Position = new Vector2(0f, -120f);
		obj.AddChild(cpuParticles2D, forceReadableName: false, Node.InternalMode.Disabled);
		CpuParticles2D cpuParticles2D2 = CreateParticles(10, 0.8, new Vector2(20f, 40f), new Vector2(-1f, -0.3f), 30f, 50f, 160f, 0.3f, 0.6f, new Color(0.25f, 0.7f, 1f, 0.35f), new Color(0.2f, 0.65f, 1f, 0f), "res://images/vfx/stance/calm_orb.png");
		cpuParticles2D2.Position = new Vector2(60f, -100f);
		obj.AddChild(cpuParticles2D2, forceReadableName: false, Node.InternalMode.Disabled);
		return obj;
	}

	public static Node2D CreateWrathVfx()
	{
		Node2D obj = new Node2D
		{
			Name = "WrathVfx"
		};
		CpuParticles2D cpuParticles2D = CreateFogParticles(8, 2.0, new Vector2(30f, 30f), 2f, 2.7f, new Color(0.65f, 0f, 0.15f), 0.15f, 40f);
		cpuParticles2D.Position = new Vector2(0f, -110f);
		obj.AddChild(cpuParticles2D, forceReadableName: false, Node.InternalMode.Disabled);
		CpuParticles2D cpuParticles2D2 = CreateParticles(16, 1.5, new Vector2(35f, 15f), new Vector2(0f, -1f), 25f, 30f, 70f, 0.15f, 0.45f, new Color(0.8f, 0f, 0.1f, 0.4f), new Color(0.6f, 0f, 0.15f, 0f));
		cpuParticles2D2.Position = new Vector2(0f, -85f);
		obj.AddChild(cpuParticles2D2, forceReadableName: false, Node.InternalMode.Disabled);
		return obj;
	}

	public static Node2D CreateDivinityVfx()
	{
		Node2D obj = new Node2D
		{
			Name = "DivinityVfx"
		};
		CpuParticles2D cpuParticles2D = CreateFogParticles(10, 2.0, new Vector2(30f, 35f), 2f, 2.7f, new Color(0.65f, 0.05f, 0.65f), 0.15f, 40f);
		cpuParticles2D.Position = new Vector2(0f, -130f);
		obj.AddChild(cpuParticles2D, forceReadableName: false, Node.InternalMode.Disabled);
		CpuParticles2D cpuParticles2D2 = CreateParticles(14, 1.5, new Vector2(25f, 25f), new Vector2(0f, -1f), 180f, 15f, 45f, 0.15f, 0.4f, new Color(0.9f, 0.6f, 0.9f, 0.4f), new Color(0.8f, 0.5f, 0.8f, 0f));
		cpuParticles2D2.Position = new Vector2(0f, -130f);
		obj.AddChild(cpuParticles2D2, forceReadableName: false, Node.InternalMode.Disabled);
		return obj;
	}

	public static Node2D CreateForeseenVfx()
	{
		Node2D obj = new Node2D
		{
			Name = "ForeseenVfx"
		};
		CpuParticles2D cpuParticles2D = CreateFogParticles(9, 2.0, new Vector2(32f, 38f), 1.8f, 2.5f, new Color(0.1f, 0.85f, 0.75f), 0.14f, 28f);
		cpuParticles2D.Position = new Vector2(0f, -120f);
		obj.AddChild(cpuParticles2D, forceReadableName: false, Node.InternalMode.Disabled);
		CpuParticles2D cpuParticles2D2 = CreateParticles(12, 1.2, new Vector2(28f, 35f), new Vector2(0f, -1f), 120f, 18f, 55f, 0.18f, 0.4f, new Color(0.25f, 0.95f, 0.85f, 0.38f), new Color(0.4f, 0.8f, 1f, 0f));
		cpuParticles2D2.Position = new Vector2(0f, -120f);
		obj.AddChild(cpuParticles2D2, forceReadableName: false, Node.InternalMode.Disabled);
		return obj;
	}

	private static Texture2D? LoadCached(ref Texture2D? cache, string path)
	{
		if (cache != null)
		{
			return cache;
		}
		cache = WatcherTextureHelper.LoadTexture(path);
		return cache;
	}

	private static CpuParticles2D CreateFogParticles(int amount, double lifetime, Vector2 emissionExtents, float scaleMin, float scaleMax, Color peakColor, float peakAlpha, float angularVelocity)
	{
		CpuParticles2D cpuParticles2D = new CpuParticles2D();
		cpuParticles2D.Emitting = true;
		cpuParticles2D.Amount = amount;
		cpuParticles2D.Lifetime = lifetime;
		cpuParticles2D.OneShot = false;
		cpuParticles2D.Preprocess = lifetime;
		cpuParticles2D.Randomness = 1f;
		cpuParticles2D.LifetimeRandomness = 0.3;
		cpuParticles2D.LocalCoords = true;
		Texture2D texture2D = LoadCached(ref _exhaustTexture, "res://images/vfx/stance/exhaust_l.png");
		if (texture2D != null)
		{
			cpuParticles2D.Texture = texture2D;
		}
		CanvasItemMaterial canvasItemMaterial = new CanvasItemMaterial();
		canvasItemMaterial.BlendMode = CanvasItemMaterial.BlendModeEnum.Add;
		cpuParticles2D.Material = canvasItemMaterial;
		cpuParticles2D.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;
		cpuParticles2D.EmissionRectExtents = emissionExtents;
		cpuParticles2D.Direction = new Vector2(0f, -1f);
		cpuParticles2D.Spread = 30f;
		cpuParticles2D.Gravity = Vector2.Zero;
		cpuParticles2D.InitialVelocityMin = 2f;
		cpuParticles2D.InitialVelocityMax = 8f;
		cpuParticles2D.ScaleAmountMin = scaleMin;
		cpuParticles2D.ScaleAmountMax = scaleMax;
		cpuParticles2D.AngularVelocityMin = 0f - angularVelocity;
		cpuParticles2D.AngularVelocityMax = angularVelocity;
		Gradient gradient = new Gradient();
		gradient.Offsets = new float[3] { 0f, 0.3f, 1f };
		gradient.Colors = new Color[3]
		{
			new Color(peakColor.R, peakColor.G, peakColor.B, 0f),
			new Color(peakColor.R, peakColor.G, peakColor.B, peakAlpha),
			new Color(peakColor.R, peakColor.G, peakColor.B, 0f)
		};
		cpuParticles2D.ColorRamp = gradient;
		return cpuParticles2D;
	}

	private static CpuParticles2D CreateParticles(int amount, double lifetime, Vector2 emissionExtents, Vector2 direction, float spread, float velocityMin, float velocityMax, float scaleMin, float scaleMax, Color colorStart, Color colorEnd, string? texturePath = null)
	{
		CpuParticles2D cpuParticles2D = new CpuParticles2D();
		cpuParticles2D.Emitting = true;
		cpuParticles2D.Amount = amount;
		cpuParticles2D.Lifetime = lifetime;
		cpuParticles2D.OneShot = false;
		cpuParticles2D.Preprocess = lifetime;
		cpuParticles2D.Randomness = 1f;
		cpuParticles2D.LifetimeRandomness = 0.4;
		cpuParticles2D.LocalCoords = true;
		Texture2D texture2D = ((texturePath == null) ? LoadCached(ref _glowSparkTexture, "res://images/vfx/stance/glow_spark.png") : LoadCached(ref _calmOrbTexture, texturePath));
		if (texture2D != null)
		{
			cpuParticles2D.Texture = texture2D;
		}
		CanvasItemMaterial canvasItemMaterial = new CanvasItemMaterial();
		canvasItemMaterial.BlendMode = CanvasItemMaterial.BlendModeEnum.Add;
		cpuParticles2D.Material = canvasItemMaterial;
		cpuParticles2D.EmissionShape = CpuParticles2D.EmissionShapeEnum.Rectangle;
		cpuParticles2D.EmissionRectExtents = emissionExtents;
		cpuParticles2D.Direction = direction;
		cpuParticles2D.Spread = spread;
		cpuParticles2D.Gravity = Vector2.Zero;
		cpuParticles2D.InitialVelocityMin = velocityMin;
		cpuParticles2D.InitialVelocityMax = velocityMax;
		cpuParticles2D.ScaleAmountMin = scaleMin;
		cpuParticles2D.ScaleAmountMax = scaleMax;
		Gradient gradient = new Gradient();
		gradient.SetColor(0, colorStart);
		gradient.SetColor(1, colorEnd);
		cpuParticles2D.ColorRamp = gradient;
		return cpuParticles2D;
	}
}
public sealed class MarkPower : PowerModel
{
	public override PowerType Type => PowerType.Debuff;

	public override PowerStackType StackType => PowerStackType.Counter;
}
public sealed class MentalFortressPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;
}
public sealed class NirvanaPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;
}
public sealed class RushdownPower : PowerModel
{
	internal int PendingDraws;

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override async Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? source)
	{
		if (PendingDraws > 0 && oldPileType == PileType.Play && card.Owner == base.Owner.Player)
		{
			int draws = PendingDraws;
			PendingDraws = 0;
			await WatcherCombatHelper.RunWithHookContext(base.Owner.Player, (PlayerChoiceContext choiceCtx) => CardPileCmd.Draw(choiceCtx, draws, base.Owner.Player));
		}
	}
}
public sealed class ForesightPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	internal async Task BeforeHandDrawCompat(Player player, PlayerChoiceContext choiceContext)
	{
		if (player == base.Owner.Player)
		{
			await WatcherCombatHelper.Scry(choiceContext, player, base.Amount);
		}
	}
}
public sealed class StudyPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		if (side == base.Owner.Side && base.Owner.Player != null)
		{
			for (int i = 0; i < base.Amount; i++)
			{
				await WatcherCardPileCompat.AddGeneratedCardToCombat(await WatcherCombatHelper.CreateWatcherCard<WatcherInsight>(base.Owner.Player), PileType.Draw, addedByPlayer: true, CardPilePosition.Random);
			}
		}
	}
}
public sealed class LikeWaterPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		if (side == base.Owner.Side && base.Owner.Player != null && WatcherCombatHelper.IsInStance<Calm>(base.Owner))
		{
			await CreatureCmd.GainBlock(base.Owner, base.Amount, ValueProp.Unpowered, null);
		}
	}
}
public sealed class EstablishmentPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		if (side != base.Owner.Side)
		{
			return Task.CompletedTask;
		}
		CombatState combatState = WatcherCreatureCompat.GetCombatState(base.Owner);
		if (combatState == null)
		{
			return Task.CompletedTask;
		}
		Player player = base.Owner.Player;
		if (player == null)
		{
			return Task.CompletedTask;
		}
		if (WatcherHookCompat.ShouldFlush(combatState, player))
		{
			return Task.CompletedTask;
		}
		bool flag = false;
		foreach (AbstractModel hookListener in WatcherHookCompat.GetHookListeners(combatState))
		{
			if ((hookListener is RetainHandPower || hookListener.GetType().Name == "RingingTriangle") && !hookListener.ShouldFlush(player))
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			return Task.CompletedTask;
		}
		foreach (CardModel card in PileType.Hand.GetPile(player).Cards)
		{
			if (!card.ShouldRetainThisTurn)
			{
				card.EnergyCost.AddThisCombat(-base.Amount, reduceOnly: true);
			}
		}
		return Task.CompletedTask;
	}
}
public sealed class MasterRealityPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Single;

	public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
	{
		if (card.Owner?.Creature == base.Owner && card.IsUpgradable)
		{
			CardCmd.Upgrade(card);
		}
		return Task.CompletedTask;
	}
}
public sealed class DevaPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new EnergyVar("GainEnergy", 1) };

	private List<int> Instances => GetInternalData<List<int>>();

	protected override object? InitInternalData()
	{
		return new List<int>();
	}

	internal void AddInstance(int amount)
	{
		if (amount > 0)
		{
			Instances.Add(amount);
			SyncAmount(silent: false);
		}
	}

	public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
	{
		if (Instances.Count == 0)
		{
			Instances.Add(Math.Max(1, base.Amount));
			SyncAmount(silent: true);
		}
		await Task.CompletedTask;
	}

	public override async Task AfterRemoved(Creature oldOwner)
	{
		WatcherAttackVfxHelper.StopDevaEyeSweep(oldOwner);
		await Task.CompletedTask;
	}

	public override async Task AfterEnergyReset(Player player)
	{
		if (player == base.Owner.Player)
		{
			if (Instances.Count == 0)
			{
				Instances.Add(Math.Max(1, base.Amount));
			}
			await PlayerCmd.GainEnergy(Instances.Sum(), player);
			for (int i = 0; i < Instances.Count; i++)
			{
				Instances[i]++;
			}
			SyncAmount(silent: true);
		}
	}

	private void SyncAmount(bool silent)
	{
		SetAmount(Instances.Sum(), silent);
	}
}
public sealed class EndTurnDeathPower : PowerModel
{
	private int _appliedOnTurn;

	public override PowerType Type => PowerType.Debuff;

	public override PowerStackType StackType => PowerStackType.Single;

	protected override bool IsVisibleInternal => true;

	public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
	{
		_appliedOnTurn = WatcherCreatureCompat.GetPlayerTurnCounter(base.Owner);
		await Task.CompletedTask;
	}

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player == base.Owner.Player && WatcherCreatureCompat.GetPlayerTurnCounter(base.Owner) != _appliedOnTurn)
		{
			await CreatureCmd.Damage(choiceContext, base.Owner, 99999m, ValueProp.Unblockable | ValueProp.Unpowered, (CardModel)null, (CardPlay)null);
			await PowerCmd.Remove(this);
		}
	}
}
public sealed class BlockReturnPower : PowerModel
{
	public override PowerType Type => PowerType.Debuff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
	{
		if (target == base.Owner && dealer != null && dealer != base.Owner && cardSource != null && cardSource.Type == CardType.Attack && props.IsPoweredAttack() && result.TotalDamage > 0)
		{
			Player player = dealer.Player ?? dealer.PetOwner ?? base.Applier?.Player;
			if (player != null)
			{
				await CreatureCmd.GainBlock(player.Creature, base.Amount, ValueProp.Unpowered, null);
			}
		}
	}
}
public sealed class WaveOfTheHandPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override async Task AfterBlockGained(Creature creature, decimal amount, ValueProp props, CardModel? cardSource)
	{
		CombatState combatState = WatcherCreatureCompat.GetCombatState(base.Owner);
		if (creature == base.Owner && !(amount <= 0m) && base.Owner.Player != null && combatState != null)
		{
			await WatcherPowerCmdCompat.Apply<WeakPower>(combatState.HittableEnemies, base.Amount, base.Owner, cardSource);
		}
	}

	public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		if (side == base.Owner.Side)
		{
			await PowerCmd.Remove(this);
		}
	}
}
public sealed class OmniscienceDoublePower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	protected override bool IsVisibleInternal => false;

	public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
	{
		if (card.Owner.Creature != base.Owner)
		{
			return playCount;
		}
		return playCount + 1;
	}

	public override async Task AfterModifyingCardPlayCount(CardModel card)
	{
		await PowerCmd.Remove(this);
	}
}
public sealed class RestfulPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Single;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromPower<Calm>(null) };

	public decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
	{
		if (target != base.Owner || dealer == base.Owner || amount <= 0m || !WatcherCombatHelper.IsInStance<Calm>(base.Owner))
		{
			return 1m;
		}
		decimal num = (decimal)Math.Clamp(base.Amount, 0, 100) / 100m;
		return Math.Max(1m, Math.Floor(amount * (1m - num))) / amount;
	}

	public override async Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
	{
		if (player == base.Owner.Player)
		{
			await PowerCmd.Remove(this);
		}
	}
}
public sealed class GospelPower : PowerModel
{
	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[2]
	{
		HoverTipFactory.FromPower<Mantra>(null),
		HoverTipFactory.FromPower<DoomPower>(null)
	};

	public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
	{
		if (dealer != base.Owner || !props.HasFlag(ValueProp.Move) || props.HasFlag(ValueProp.Unpowered) || result.UnblockedDamage <= 0)
		{
			return;
		}
		Player player = base.Owner.Player;
		await WatcherCombatHelper.GainMantra(player, base.Amount, null);
		CombatState combatState = WatcherCreatureCompat.GetCombatState(base.Owner);
		if ((combatState?.Players.Count ?? 1) > 1)
		{
			int mantraGainedThisTurn = WatcherCombatHelper.GetMantraGainedThisTurn(player);
			if (mantraGainedThisTurn > 0 && combatState != null)
			{
				await WatcherPowerCmdCompat.Apply<DoomPower>(combatState.HittableEnemies, mantraGainedThisTurn, base.Owner, null);
			}
		}
	}
}
public sealed class DivineDoomPower : PowerModel
{
	private bool _usedThisTurn;

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Single;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromPower<DoomPower>(null) };

	public override Task AfterPlayerTurnStartEarly(PlayerChoiceContext choiceContext, Player player)
	{
		if (player == base.Owner.Player)
		{
			_usedThisTurn = false;
		}
		return Task.CompletedTask;
	}

	public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
	{
		if (dealer == base.Owner && props.HasFlag(ValueProp.Move) && !props.HasFlag(ValueProp.Unpowered) && result.UnblockedDamage > 0 && !_usedThisTurn && base.Owner.HasPower<Divinity>())
		{
			_usedThisTurn = true;
			int num = (int)Math.Ceiling((decimal)target.MaxHp * 0.05m);
			CombatState combatState = WatcherCreatureCompat.GetCombatState(base.Owner);
			if (num > 0 && combatState != null)
			{
				await WatcherPowerCmdCompat.Apply<DoomPower>(combatState.HittableEnemies, num, base.Owner, null);
			}
		}
	}
}
public sealed class ColdObservationPower : PowerModel
{
	private CardModel? _lastProcessedCard;

	public override PowerType Type => PowerType.Buff;

	public override PowerStackType StackType => PowerStackType.Counter;

	public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
	{
		if (dealer == base.Owner && props.HasFlag(ValueProp.Move) && !props.HasFlag(ValueProp.Unpowered) && result.TotalDamage > 0)
		{
			await CreatureCmd.GainBlock(base.Owner, result.TotalDamage, ValueProp.Unpowered, null);
			if (cardSource != _lastProcessedCard)
			{
				_lastProcessedCard = cardSource;
				await WatcherPowerCmdCompat.ModifyAmount(this, -1m, null, null);
			}
		}
	}
}
public sealed class CannotChangeStancePower : PowerModel
{
	public override PowerType Type => PowerType.Debuff;

	public override PowerStackType StackType => PowerStackType.Single;
}
public sealed class YangDexterityPower : TemporaryDexterityPower
{
	public override AbstractModel OriginModel => ModelDb.Relic<Yang>();
}
public sealed class PureWater : WatcherRelic
{
	public override RelicRarity Rarity => RelicRarity.Starter;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => new IHoverTip[] { HoverTipFactory.FromKeyword(CardKeyword.Exhaust) };

	public PureWater()
		: base("clean_water")
	{
	}

	public override async Task BeforeCombatStart()
	{
		CombatState combatState = WatcherCreatureCompat.GetCombatState(base.Owner.Creature);
		if (combatState != null)
		{
			Flash();
			await WatcherCardPileCompat.AddGeneratedCardToCombat(combatState.CreateCard<WatcherMiracle>(base.Owner), PileType.Hand, addedByPlayer: true);
		}
	}
}
public sealed class Damaru : WatcherRelic
{
	public override RelicRarity Rarity => RelicRarity.Common;

	protected override IEnumerable<DynamicVar> CanonicalVars => new DynamicVar[] { new DynamicVar("Mantra", 1m) };

	public Damaru()
		: base("damaru")
	{
	}

	public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player == base.Owner)
		{
			Flash();
			await WatcherCombatHelper.GainMantra(base.Owner, base.DynamicVars["Mantra"].IntValue, null);
		}
	}
}
public sealed class TeardropLocket : WatcherRelic
{
	public override RelicRarity Rarity => RelicRarity.Uncommon;

	public TeardropLocket()
		: base("tear_drop_locket")
	{
	}

	public override async Task BeforeCombatStart()
	{
		Flash();
		await WatcherCombatHelper.EnterCalm(base.Owner, null);
	}
}
public sealed class HolyWater : WatcherRelic
{
	public override RelicRarity Rarity => RelicRarity.Starter;

	public HolyWater()
		: base("holy_water")
	{
	}

	public override async Task BeforeCombatStart()
	{
		CombatState combat = WatcherCreatureCompat.GetCombatState(base.Owner.Creature);
		if (combat != null)
		{
			Flash();
			for (int i = 0; i < 3; i++)
			{
				await WatcherCardPileCompat.AddGeneratedCardToCombat(combat.CreateCard<WatcherMiracle>(base.Owner), PileType.Hand, addedByPlayer: true);
			}
		}
	}
}
public sealed class GoldenEye : WatcherRelic
{
	public override RelicRarity Rarity => RelicRarity.Rare;

	public GoldenEye()
		: base("golden_eye")
	{
	}
}
public sealed class Melange : WatcherRelic
{
	public override RelicRarity Rarity => RelicRarity.Shop;

	public Melange()
		: base("melange")
	{
	}

	public override async Task AfterShuffle(PlayerChoiceContext choiceContext, Player shuffler)
	{
		if (shuffler == base.Owner)
		{
			Flash();
			await WatcherCombatHelper.Scry(choiceContext, base.Owner, 3);
		}
	}
}
public sealed class VioletLotus : WatcherRelic
{
	public override RelicRarity Rarity => RelicRarity.Ancient;

	public VioletLotus()
		: base("violet_lotus")
	{
	}
}
public sealed class CloakClasp_P : WatcherRelic
{
	public override RelicRarity Rarity => RelicRarity.None;

	public CloakClasp_P()
		: base("clasp")
	{
	}

	public override async Task BeforeSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
	{
		if (side == base.Owner.Creature.Side)
		{
			int count = PileType.Hand.GetPile(base.Owner).Cards.Count;
			if (count > 0)
			{
				Flash();
				await CreatureCmd.GainBlock(base.Owner.Creature, count, ValueProp.Unpowered, null);
			}
		}
	}
}
public sealed class CeramicFish_P : WatcherRelic
{
	public override RelicRarity Rarity => RelicRarity.Common;

	public CeramicFish_P()
		: base("ceramic_fish")
	{
	}

	public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? source)
	{
		if (card.Owner == base.Owner)
		{
			CardPile? pile = card.Pile;
			if (pile != null && pile.Type == PileType.Deck && oldPileType != PileType.Deck)
			{
				base.Owner.Gold += 9;
			}
		}
		return Task.CompletedTask;
	}
}
public sealed class Yang : WatcherRelic
{
	public override RelicRarity Rarity => RelicRarity.Uncommon;

	public Yang()
		: base("duality")
	{
	}

	public override async Task BeforeCardPlayed(CardPlay cardPlay)
	{
		if (cardPlay.Card.Owner == base.Owner && cardPlay.Card.Type == CardType.Attack)
		{
			Flash();
			await WatcherPowerCmdCompat.Apply<YangDexterityPower>(base.Owner.Creature, 1m, base.Owner.Creature, cardPlay.Card);
		}
	}
}
internal static class WatcherHoverTips
{
	internal static IHoverTip Stance { get; } = new HoverTip(new LocString("powers", "STANCE.title"), new LocString("powers", "STANCE.description"));

	internal static IHoverTip Prophecy { get; } = new HoverTip(new LocString("powers", "PROPHECY.title"), new LocString("powers", "PROPHECY.description"));

	internal static IHoverTip Scry { get; } = new HoverTip(new LocString("powers", "SCRY.title"), new LocString("powers", "SCRY.description"));

	internal static IHoverTip Confusion { get; } = HoverTipFactory.FromPower<ConfusionPower>(null);

	internal static IHoverTip Finality { get; } = new HoverTip(new LocString("powers", "FINALITY.title"), new LocString("powers", "FINALITY.description"));

	internal static IHoverTip Full { get; } = new HoverTip(new LocString("powers", "FULL_FINALITY.title"), new LocString("powers", "FULL_FINALITY.description"));

	internal static IHoverTip Enchantment { get; } = new HoverTip(new LocString("powers", "ENCHANTMENT.title"), new LocString("powers", "ENCHANTMENT.description"));

	internal static IHoverTip Directed { get; } = new HoverTip(new LocString("powers", "DIRECTED.title"), new LocString("powers", "DIRECTED.description"));
}
public abstract class WatcherCard : CardModel
{
	internal const string ProphecyGoldOverlayScenePath = "res://scenes/cards/overlays/watcher_prophecy_gold.tscn";

	public override bool HasBuiltInOverlay
	{
		get
		{
			if (!base.HasBuiltInOverlay)
			{
				return ShouldUseProphecyGoldOverlay(this);
			}
			return true;
		}
	}

	protected override IEnumerable<string> ExtraRunAssetPaths
	{
		get
		{
			if (!ShouldUseProphecyGoldOverlay(this))
			{
				return base.ExtraRunAssetPaths;
			}
			return base.ExtraRunAssetPaths.Append("res://scenes/cards/overlays/watcher_prophecy_gold.tscn");
		}
	}

	public override CardPoolModel Pool => ModelDb.CardPool<WatcherCardPool>();

	protected new CombatState? CombatState => WatcherCardCompat.GetCombatState(this);

	internal string AssetEntry
	{
		get
		{
			string entry = base.Id.Entry;
			string text;
			if (!entry.StartsWith("WATCHER_", StringComparison.Ordinal))
			{
				text = entry;
			}
			else
			{
				string text2 = entry;
				int length = "WATCHER_".Length;
				text = text2.Substring(length, text2.Length - length);
			}
			return text.ToLower();
		}
	}

	public override string PortraitPath => "res://images/packed/card_portraits/watcher/" + AssetEntry + ".png";

	public override string BetaPortraitPath => "res://images/packed/card_portraits/watcher/beta/" + AssetEntry + ".png";

	protected override string PortraitPngPath => PortraitPath;

	public override IEnumerable<string> AllPortraitPaths => new string[2] { PortraitPath, BetaPortraitPath };

	protected WatcherCard(int canonicalEnergyCost, CardType type, CardRarity rarity, TargetType targetType, bool shouldShowInCardLibrary = true)
		: base(canonicalEnergyCost, type, rarity, targetType, shouldShowInCardLibrary)
	{
	}

	internal static bool ShouldUseProphecyGoldOverlay(CardModel? card)
	{
		if (card == null || card.Rarity == CardRarity.Ancient)
		{
			return false;
		}
		bool flag = card is IProphecyCard;
		if (!flag)
		{
			bool flag2;
			switch (card.GetType().Name)
			{
			case "WatcherBrillianceV2":
			case "WatcherJudgmentV2":
			case "WatcherWishV2":
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			flag = flag2;
		}
		return flag;
	}

	public override async Task OnEnqueuePlayVfx(Creature? target)
	{
		if (ShouldPlayPowerUpVfx(this))
		{
			NPowerUpVfx.CreateNormal(base.Owner.Creature);
		}
		else if (ShouldPlayGhostlyPowerUpVfx(this))
		{
			NPowerUpVfx.CreateGhostly(base.Owner.Creature);
		}
		else if (target != null && ShouldPlaySpookyTargetVfx(this))
		{
			VfxCmd.PlayOnCreatureCenter(target, "vfx/vfx_spooky_scream");
		}
		if (Type == CardType.Skill)
		{
			await CreatureCmd.TriggerAnim(base.Owner.Creature, "Cast", base.Owner.Character.CastAnimDelay);
		}
		await base.OnEnqueuePlayVfx(target);
	}

	private static bool ShouldPlayPowerUpVfx(CardModel card)
	{
		switch (card.GetType().Name)
		{
		case "WatcherEnlightenFate":
		case "WatcherGlimpseFuture":
		case "WatcherDestinyAwaits":
		case "WatcherProstrateV2":
		case "WatcherScrawl_P_V2":
		case "WatcherGuard":
		case "WatcherVaultV2":
		case "WatcherWishV2":
			return true;
		default:
			return false;
		}
	}

	private static bool ShouldPlayGhostlyPowerUpVfx(CardModel card)
	{
		switch (card.GetType().Name)
		{
		case "WatcherForesightV2":
		case "WatcherNirvanaV2":
		case "WatcherOmniscienceV2":
			return true;
		default:
			return false;
		}
	}

	private static bool ShouldPlaySpookyTargetVfx(CardModel card)
	{
		switch (card.GetType().Name)
		{
		case "WatcherPrescience":
		case "WatcherForetell":
		case "WatcherPortent":
		case "WatcherOmenLash":
		case "WatcherRewriteFate":
		case "WatcherJudgmentV2":
			return true;
		default:
			return false;
		}
	}
}
public abstract class WatcherRelic(string assetName) : RelicModel()
{
	public override string PackedIconPath => "res://images/relics/" + assetName + ".png";

	protected override string PackedIconOutlinePath => "res://images/relics/outline/" + assetName + ".png";

	protected override string BigIconPath => PackedIconPath;
}
[ModInitializer("Init")]
public static class WatcherBootstrap
{
	private static bool _initialized;


	public static void Init()
	{
		if (_initialized)
		{
			return;
		}
		_initialized = true;
		// Harmony 补丁已由 STS2Weaver 构建期静态织入(iOS NativeAOT 无运行时打补丁),此处只做订阅与模型注册。
		WatcherEnchantStack.RegisterSubscriptions();
		ModHelper.AddModelToPool<EventRelicPool, HolyWater>();
		Type[] array = WatcherDerivativeTokens.Types;
		foreach (Type modelType in array)
		{
			ModHelper.AddModelToPool(typeof(TokenCardPool), modelType);
		}
		Log.Info("[Watcher] 已初始化观者模组。");
	}

	public static void ModManagerInitializePostfix()
	{
		// iOS: ModManager.Initialize 完成后挂载资源包并初始化(静态织入版)。pck 由 push-mod.sh 推到 Documents/Watcher.pck。
		try
		{
			string pckPath = ProjectSettings.GlobalizePath("user://Watcher.pck");
			if (Godot.FileAccess.FileExists(pckPath))
			{
				bool ok = ProjectSettings.LoadResourcePack(pckPath, replaceFiles: true);
				Log.Info("[Watcher] Watcher.pck 挂载: " + ok);
			}
			else
			{
				Log.Warn("[Watcher] 未找到 user://Watcher.pck,观者美术资源将缺失(push-mod.sh 推送)");
			}
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] Watcher.pck 挂载失败: " + ex.Message);
		}
		Init();
		RegisterBuiltinModEntry();
	}

	private static void RegisterBuiltinModEntry()
	{
		// iOS 绕过动态 mod 加载后,游戏的 mod 本地化管线
		// (LocManager.LoadTablesFromPath → ModManager.GetModdedLocTables → res://Watcher/localization/{lang}/{file})
		// 和 Mods 界面都从 ModManager._mods 读取 —— 把 Watcher 注册为"已加载"的内置 mod 条目。
		// 游戏主程序集被 Godot targets 整库 root,私有静态字段反射可用。
		try
		{
			FieldInfo modsField = typeof(ModManager).GetField("_mods", BindingFlags.Static | BindingFlags.NonPublic);
			if (modsField == null || modsField.GetValue(null) is not IList mods)
			{
				Log.Warn("[Watcher] ModManager._mods 不可用,mod 本地化管线不可见");
				return;
			}
			foreach (object existing in mods)
			{
				if (existing is Mod existingMod && existingMod.manifest?.id == "Watcher")
				{
					return;
				}
			}
			Mod mod = new Mod
			{
				manifest = new ModManifest
				{
					id = "Watcher",
					name = "Watcher",
					author = "Boninall",
					version = "0.9.29",
					hasPck = true,
					hasDll = true,
					affectsGameplay = true,
					minGameVersion = "0.111.0"
				},
				state = ModLoadState.Loaded,
				modSource = ModSource.None,
				path = ""
			};
			// 游戏按 Mod.assemblies 判定模型类型归属(动态加载时 TryLoadMod 会把 mod dll 加进来)。
			// 不关联的话 ModelDb 注册每个模型都会报 "not associated with any mod" 且多人排序可能出错。
			mod.assemblies.Add(typeof(WatcherBootstrap).Assembly);
			if (MegaCrit.Sts2.Core.Debug.SemanticVersion.TryFromString("0.9.29", out MegaCrit.Sts2.Core.Debug.SemanticVersion ver))
			{
				mod.version = ver;
			}
			mods.Add(mod);
			Log.Info("[Watcher] 已注册内置 mod 条目(loc 与 Mods 界面可见)");
		}
		catch (Exception ex)
		{
			Log.Error("[Watcher] 注册内置 mod 条目失败: " + ex.Message);
		}
	}

}
public class Watcher : CharacterModel
{
	internal const string LowHealthIdleAnim = "LowHealthIdle";

	public override CharacterGender Gender => CharacterGender.Feminine;

	protected override CharacterModel? UnlocksAfterRunAs => ModelDb.Character<Defect>();

	public override Color NameColor => new Color("9E68FF");

	public override int StartingHp => 72;

	public override int StartingGold => 99;

	public override float AttackAnimDelay => 0.15f;

	public override float CastAnimDelay => 0.25f;

	protected override string MapMarkerPath => "res://images/packed/map/icons/map_marker_watcher.png";

	public override CardPoolModel CardPool => ModelDb.CardPool<WatcherCardPool>();

	public override PotionPoolModel PotionPool => ModelDb.PotionPool<WatcherPotionPool>();

	public override RelicPoolModel RelicPool => ModelDb.RelicPool<WatcherRelicPool>();

	public override IEnumerable<CardModel> StartingDeck => new CardModel[10]
	{
		ModelDb.Card<WatcherStrike_P>(),
		ModelDb.Card<WatcherStrike_P>(),
		ModelDb.Card<WatcherStrike_P>(),
		ModelDb.Card<WatcherStrike_P>(),
		ModelDb.Card<WatcherDefend_P>(),
		ModelDb.Card<WatcherDefend_P>(),
		ModelDb.Card<WatcherDefend_P>(),
		ModelDb.Card<WatcherDefend_P>(),
		ModelDb.Card<WatcherEruption_P>(),
		ModelDb.Card<WatcherVigilance>()
	};

	public override IReadOnlyList<RelicModel> StartingRelics => new RelicModel[] { ModelDb.Relic<PureWater>() };

	public override Color EnergyLabelOutlineColor => new Color("4E2A7AFF");

	public override Color DialogueColor => new Color("3A2254");

	public override Color MapDrawingColor => new Color("9E68FF");

	public override Color RemoteTargetingLineColor => new Color("C099FF");

	public override Color RemoteTargetingLineOutline => new Color("4E2A7AFF");

	public override string CharacterSelectSfx => "";

	public override string CharacterTransitionSfx => "event:/sfx/ui/wipe_necrobinder";

	protected virtual string? CharacterSkeletonDataPath => null;

	protected virtual string? CharacterEyeBoneName => null;

	protected virtual Vector2 CharacterEyeOffset => new Vector2(5f, -5f);

	protected virtual float CharacterEyeRotation => 2.618f;

	protected virtual float CharacterEyeScale => 0.8f;

	protected virtual float SkeletonScale => 1.05f;

	public override List<string> GetArchitectAttackVfx()
	{
		int num = 3;
		List<string> list = new List<string>(num);
		CollectionsMarshal.SetCount(list, num);
		Span<string> span = CollectionsMarshal.AsSpan(list);
		int num2 = 0;
		span[num2] = "vfx/vfx_attack_slash";
		num2++;
		span[num2] = "vfx/vfx_bloody_impact";
		num2++;
		span[num2] = "vfx/vfx_attack_blunt";
		return list;
	}

	public virtual CreatureAnimator GenerateAnimator(MegaSprite controller, Creature creature)
	{
		string characterSkeletonDataPath = CharacterSkeletonDataPath;
		if (characterSkeletonDataPath != null)
		{
			WatcherSkeletonHelper.ApplySkeletonDataPath(controller, characterSkeletonDataPath, "prophet");
		}
		else
		{
			WatcherSkeletonHelper.ApplySkeletonVariant(controller);
		}
		if (controller.BoundObject is Node2D node2D)
		{
			node2D.Scale = Vector2.One * SkeletonScale;
			if (characterSkeletonDataPath != null && WatcherModSettings.UseCommunitySkeleton)
			{
				node2D.Position += WatcherModSettings.BeautifiedProphetRigCenterOffset * SkeletonScale;
			}
			if (characterSkeletonDataPath == null)
			{
				WatcherSkeletonHelper.EnsureEyeTop(node2D);
			}
			else if (!WatcherModSettings.UseCommunitySkeleton)
			{
				WatcherSkeletonHelper.BindEyeToBone(node2D, CharacterEyeBoneName, CharacterEyeOffset, CharacterEyeRotation, CharacterEyeScale);
			}
			else
			{
				WatcherSkeletonHelper.HideEye(node2D);
			}
		}
		AnimState animState = new AnimState("Idle", isLooping: true);
		AnimState state = new AnimState("Dead");
		AnimState animState2 = new AnimState("Hit");
		AnimState animState3 = new AnimState("Attack");
		AnimState animState4 = new AnimState("Cast");
		AnimState animState5 = new AnimState("SignatureMove");
		animState2.NextState = animState;
		animState3.NextState = animState;
		animState4.NextState = animState;
		animState5.NextState = animState;
		CreatureAnimator creatureAnimator;
		if (controller.HasAnimation("LowHealthIdle"))
		{
			AnimState animState6 = new AnimState("LowHealthIdle", isLooping: true);
			AnimState[] array = new AnimState[4] { animState2, animState3, animState4, animState5 };
			for (int i = 0; i < array.Length; i++)
			{
				array[i].AddNextState(animState6, (Func<bool>)(() => base.IsLowHealth(creature)));
			}
			creatureAnimator = new CreatureAnimator(base.IsLowHealth(creature) ? animState6 : animState, controller);
			creatureAnimator.AddAnyState("Idle", animState, () => !base.IsLowHealth(creature));
			creatureAnimator.AddAnyState("Idle", animState6, () => base.IsLowHealth(creature));
		}
		else
		{
			creatureAnimator = new CreatureAnimator(animState, controller);
			creatureAnimator.AddAnyState("Idle", animState);
		}
		creatureAnimator.AddAnyState("Dead", state);
		creatureAnimator.AddAnyState("Hit", animState2);
		creatureAnimator.AddAnyState("Attack", animState3);
		creatureAnimator.AddAnyState("Cast", animState4);
		creatureAnimator.AddAnyState("SignatureMove", animState5);
		return creatureAnimator;
	}
}
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
internal sealed class WatcherPatchAttribute : Attribute
{
	public bool SkipOnAndroid { get; init; }

	public string? Reason { get; init; }
}
public sealed class WatcherCardPool : CardPoolModel
{
	public override string Title => "watcher";

	public override string EnergyColorName => "watcher";

	public override string CardFrameMaterialPath => "card_frame_purple";

	public override Color DeckEntryCardColor => new Color("9E68FF");

	public override bool IsColorless => false;

	protected override CardModel[] GenerateAllCards()
	{
		return new CardModel[83]
		{
			ModelDb.Card<WatcherStrike_P>(),
			ModelDb.Card<WatcherDefend_P>(),
			ModelDb.Card<WatcherEruption_P>(),
			ModelDb.Card<WatcherVigilance>(),
			ModelDb.Card<WatcherBowlingBash>(),
			ModelDb.Card<WatcherConsecrate>(),
			ModelDb.Card<WatcherCrescendo>(),
			ModelDb.Card<WatcherCrushJoints>(),
			ModelDb.Card<WatcherCutThroughFate>(),
			ModelDb.Card<WatcherEmptyBody>(),
			ModelDb.Card<WatcherEmptyFist>(),
			ModelDb.Card<WatcherEvaluate>(),
			ModelDb.Card<WatcherFlurryOfBlows>(),
			ModelDb.Card<WatcherFlyingSleeves>(),
			ModelDb.Card<WatcherFollowUp>(),
			ModelDb.Card<WatcherHalt>(),
			ModelDb.Card<WatcherJustLucky>(),
			ModelDb.Card<WatcherPressurePoints>(),
			ModelDb.Card<WatcherProstrate>(),
			ModelDb.Card<WatcherProtect>(),
			ModelDb.Card<WatcherSashWhip>(),
			ModelDb.Card<WatcherThirdEye>(),
			ModelDb.Card<WatcherTranquility>(),
			ModelDb.Card<WatcherColdObservation>(),
			ModelDb.Card<WatcherMockery>(),
			ModelDb.Card<WatcherPersuasion>(),
			ModelDb.Card<WatcherBattleHymn>(),
			ModelDb.Card<WatcherCarveReality>(),
			ModelDb.Card<WatcherCollect>(),
			ModelDb.Card<WatcherConclude>(),
			ModelDb.Card<WatcherDeceiveReality>(),
			ModelDb.Card<WatcherEmptyMind>(),
			ModelDb.Card<WatcherFasting2>(),
			ModelDb.Card<WatcherFearNoEvil>(),
			ModelDb.Card<WatcherForeignInfluence>(),
			ModelDb.Card<WatcherForesight>(),
			ModelDb.Card<WatcherIndignation>(),
			ModelDb.Card<WatcherInnerPeace>(),
			ModelDb.Card<WatcherLikeWater>(),
			ModelDb.Card<WatcherMeditate>(),
			ModelDb.Card<WatcherMentalFortress>(),
			ModelDb.Card<WatcherNirvana>(),
			ModelDb.Card<WatcherPerseverance>(),
			ModelDb.Card<WatcherPray>(),
			ModelDb.Card<WatcherReachHeaven>(),
			ModelDb.Card<WatcherRushdown>(),
			ModelDb.Card<WatcherSanctity>(),
			ModelDb.Card<WatcherSandsOfTime>(),
			ModelDb.Card<WatcherSignatureMove>(),
			ModelDb.Card<WatcherSimmeringFury>(),
			ModelDb.Card<WatcherStudy>(),
			ModelDb.Card<WatcherSwivel>(),
			ModelDb.Card<WatcherTalkToTheHand>(),
			ModelDb.Card<WatcherTantrum>(),
			ModelDb.Card<WatcherWallop>(),
			ModelDb.Card<WatcherWaveOfTheHand>(),
			ModelDb.Card<WatcherWeave>(),
			ModelDb.Card<WatcherWheelKick>(),
			ModelDb.Card<WatcherWindmillStrike>(),
			ModelDb.Card<WatcherWorship>(),
			ModelDb.Card<WatcherWreathOfFlame>(),
			ModelDb.Card<WatcherSanctification>(),
			ModelDb.Card<WatcherAlpha>(),
			ModelDb.Card<WatcherBlasphemy>(),
			ModelDb.Card<WatcherBrilliance>(),
			ModelDb.Card<WatcherConjureBlade>(),
			ModelDb.Card<WatcherDeusExMachina>(),
			ModelDb.Card<WatcherDevaForm>(),
			ModelDb.Card<WatcherDevotion>(),
			ModelDb.Card<WatcherEstablishment>(),
			ModelDb.Card<WatcherJudgment>(),
			ModelDb.Card<WatcherLessonLearned>(),
			ModelDb.Card<WatcherMasterReality>(),
			ModelDb.Card<WatcherOmniscience>(),
			ModelDb.Card<WatcherRagnarok>(),
			ModelDb.Card<WatcherScrawl_P>(),
			ModelDb.Card<WatcherSpiritShield>(),
			ModelDb.Card<WatcherVault>(),
			ModelDb.Card<WatcherWish_P>(),
			ModelDb.Card<WatcherCataclysm>(),
			ModelDb.Card<WatcherSerenity>(),
			ModelDb.Card<WatcherPreach>(),
			ModelDb.Card<WatcherDrawTalisman>()
		};
	}
}
public sealed class WatcherRelicPool : RelicPoolModel
{
	public override string EnergyColorName => "watcher";

	public override Color LabOutlineColor => new Color("9E68FF");

	protected override IEnumerable<RelicModel> GenerateAllRelics()
	{
		return new RelicModel[9]
		{
			ModelDb.Relic<PureWater>(),
			ModelDb.Relic<Damaru>(),
			ModelDb.Relic<Yang>(),
			ModelDb.Relic<GoldenEye>(),
			ModelDb.Relic<Melange>(),
			ModelDb.Relic<TeardropLocket>(),
			ModelDb.Relic<VioletLotus>(),
			ModelDb.Relic<CloakClasp_P>(),
			ModelDb.Relic<CeramicFish_P>()
		};
	}
}
public sealed class WatcherPotionPool : PotionPoolModel
{
	public override string EnergyColorName => "watcher";

	public override Color LabOutlineColor => new Color("9E68FF");

	protected override IEnumerable<PotionModel> GenerateAllPotions()
	{
		return new PotionModel[3]
		{
			ModelDb.Potion<Ambrosia>(),
			ModelDb.Potion<BottledMiracle>(),
			ModelDb.Potion<StancePotion>()
		};
	}
}
