﻿#region

using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;
#endregion

namespace TwistedFate
{
    internal class Program
    {
        private static Menu Config;

        private static Spell Q;
        private static float Qangle = 28*(float) Math.PI/180;
        private static Orbwalking.Orbwalker SOW;
        private static Vector2 PingLocation;
        private static int LastPingT = 0;
        private static Obj_AI_Hero Player;
        private static int CastQTick;
        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Ping(Vector2 position)
        {
            if (Environment.TickCount - LastPingT < 30 * 1000) return;
            LastPingT = Environment.TickCount;
            PingLocation = position;
            SimplePing();
            Utility.DelayAction.Add(150, SimplePing);
            Utility.DelayAction.Add(300, SimplePing);
            Utility.DelayAction.Add(400, SimplePing);
            Utility.DelayAction.Add(800, SimplePing);
        }

        private static void SimplePing()
        {
            Packet.S2C.Ping.Encoded(new Packet.S2C.Ping.Struct(PingLocation.X, PingLocation.Y, 0, 0, Packet.PingType.Fallback)).Process();
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            if (ObjectManager.Player.BaseSkinName != "TwistedFate") return;
            Player = ObjectManager.Player;
            Q = new Spell(SpellSlot.Q, 1450);
            Q.SetSkillshot(0.25f, 40f, 1000f, false, SkillshotType.SkillshotLine);

            //Make the menu
            Config = new Menu("鍒濊姹夊寲-鍗＄墝澶у笀", "TwistedFate", true);

            var TargetSelectorMenu = new Menu("鐩爣閫夋嫨", "Target Selector");
            SimpleTs.AddToMenu(TargetSelectorMenu);
            Config.AddSubMenu(TargetSelectorMenu);

            var SowMenu = new Menu("璧扮爫", "Orbwalking");
            SOW = new Orbwalking.Orbwalker(SowMenu);
            Config.AddSubMenu(SowMenu);

            /* Q */
            var q = new Menu("Q - 璁剧疆", "Q");
            q.AddItem(new MenuItem("AutoQI", "鑷姩Q鐪╂檿").SetValue(true));
            q.AddItem(new MenuItem("AutoQD", "Auto-Q dashing").SetValue(true));
            q.AddItem(new MenuItem("CastQ", "浣跨敤Q (棰勫垽)").SetValue(new KeyBind("U".ToCharArray()[0], KeyBindType.Press)));
            Config.AddSubMenu(q);

            /* W */
            var w = new Menu("W - 璁剧疆", "W");
            w.AddItem(
                new MenuItem("SelectYellow", "榛勭墝").SetValue(new KeyBind("W".ToCharArray()[0],
                    KeyBindType.Press)));
            w.AddItem(
                new MenuItem("SelectBlue", "钃濈墝").SetValue(new KeyBind("E".ToCharArray()[0], KeyBindType.Press)));
            w.AddItem(
                new MenuItem("SelectRed", "绾㈢墝").SetValue(new KeyBind("T".ToCharArray()[0], KeyBindType.Press)));
            Config.AddSubMenu(w);

            var r = new Menu("R - 璁剧疆", "R");
            r.AddItem(new MenuItem("AutoY", "浣跨敤R鍚庤嚜鍔ㄩ€変晶榛勭墝").SetValue(true));
            Config.AddSubMenu(r);

            var misc = new Menu("鏉傞」", "Misc");
            misc.AddItem(new MenuItem("PingLH", "|鎻愮ず鍙嚮鏉€鐨勬晫浜簗(鏈湴鎻愮ず)").SetValue(true));
            Config.AddSubMenu(misc);

            //Damage after combo:
            var dmgAfterComboItem = new MenuItem("DamageAfterCombo", "鏄剧ず缁勫悎浼ゅ").SetValue(true);
            Utility.HpBarDamageIndicator.DamageToUnit = ComboDamage;
            Utility.HpBarDamageIndicator.Enabled = dmgAfterComboItem.GetValue<bool>();
            dmgAfterComboItem.ValueChanged += delegate(object sender, OnValueChangeEventArgs eventArgs)
            {
                Utility.HpBarDamageIndicator.Enabled = eventArgs.GetNewValue<bool>();
            };

            /*Drawing*/
            var Drawings = new Menu("鎶€鑳借寖鍥撮€夐」", "Drawings");
            Drawings.AddItem(new MenuItem("Qcircle", "Q 鑼冨洿").SetValue(new Circle(true, Color.FromArgb(100,255,0,255))));
            Drawings.AddItem(new MenuItem("Rcircle", "R 鑼冨洿").SetValue(new Circle(true, Color.FromArgb(100, 255, 255, 255))));
            Drawings.AddItem(new MenuItem("Rcircle2", "R 鑼冨洿 (|灏忓湴鍥捐寖鍥磡)").SetValue(new Circle(true, Color.FromArgb(255, 255, 255, 255))));
            Drawings.AddItem(dmgAfterComboItem);
            Config.AddSubMenu(Drawings);

            Config.AddItem(new MenuItem("Combo", "杩炴嫑").SetValue(new KeyBind(32, KeyBindType.Press)));

            Config.AddToMainMenu();
Config.AddSubMenu(new Menu("鍒濊姹夊寲", "by chujian"));

Config.SubMenu("by chujian").AddItem(new MenuItem("qunhao", "姹夊寲缇わ細386289593"));
Config.SubMenu("by chujian").AddItem(new MenuItem("qunhao2", "濞冨▋缇わ細13497795"));
            Game.OnGameUpdate += Game_OnGameUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            Drawing.OnEndScene += DrawingOnOnEndScene;
            Obj_AI_Hero.OnProcessSpellCast += Obj_AI_Hero_OnProcessSpellCast;
            Orbwalking.BeforeAttack += OrbwalkingOnBeforeAttack;
        }

        private static void OrbwalkingOnBeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            if(args.Target is Obj_AI_Hero)
                args.Process = CardSelector.Status != SelectStatus.Selecting && Environment.TickCount - CardSelector.LastWSent > 300;
        }

        static void Obj_AI_Hero_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (sender.IsMe && args.SData.Name == "gate" && Config.Item("AutoY").GetValue<bool>())
            {
                CardSelector.StartSelecting(Cards.Yellow);
            }
        }

        private static void DrawingOnOnEndScene(EventArgs args)
        {
            var rCircle2 = Config.Item("Rcircle2").GetValue<Circle>();
            if (rCircle2.Active)
            {
                Utility.DrawCircle(ObjectManager.Player.Position, 5500, rCircle2.Color, 1, 23, true);
            }
        }

        static void Drawing_OnDraw(EventArgs args)
        {
            var qCircle = Config.Item("Qcircle").GetValue<Circle>();
            var rCircle = Config.Item("Rcircle").GetValue<Circle>();
            
            if (qCircle.Active)
            {
                Utility.DrawCircle(ObjectManager.Player.Position, Q.Range, qCircle.Color);
            }

            if (rCircle.Active)
            {
                Utility.DrawCircle(ObjectManager.Player.Position, 5500, rCircle.Color);
            }
        }


        private static int CountHits(Vector2 position, List<Vector2> points, List<int> hitBoxes)
        {
            var result = 0;

            var startPoint = ObjectManager.Player.ServerPosition.To2D();
            var originalDirection = Q.Range*(position - startPoint).Normalized();
            var originalEndPoint = startPoint + originalDirection;

            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];

                for (var k = 0; k < 3; k++)
                {
                    var endPoint = new Vector2();
                    if (k == 0) endPoint = originalEndPoint;
                    if (k == 1) endPoint = startPoint + originalDirection.Rotated(Qangle);
                    if (k == 2) endPoint = startPoint + originalDirection.Rotated(-Qangle);

                    if (point.Distance(startPoint, endPoint, true, true) <
                        (Q.Width + hitBoxes[i])*(Q.Width + hitBoxes[i]))
                    {
                        result++;
                        break;
                    }
                }
            }

            return result;
        }

        private static void CastQ(Obj_AI_Base unit, Vector2 unitPosition, int minTargets = 0)
        {
            var points = new List<Vector2>();
            var hitBoxes = new List<int>();

            var startPoint = ObjectManager.Player.ServerPosition.To2D();
            var originalDirection = Q.Range*(unitPosition - startPoint).Normalized();

            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (enemy.IsValidTarget() && enemy.NetworkId != unit.NetworkId)
                {
                    var pos = Q.GetPrediction(enemy);
                    if (pos.Hitchance >= HitChance.Medium)
                    {
                        points.Add(pos.UnitPosition.To2D());
                        hitBoxes.Add((int) enemy.BoundingRadius);
                    }
                }
            }


            var posiblePositions = new List<Vector2>();

            for (var i = 0; i < 3; i++)
            {
                if (i == 0) posiblePositions.Add(unitPosition + originalDirection.Rotated(0));
                if (i == 1) posiblePositions.Add(startPoint + originalDirection.Rotated(Qangle));
                if (i == 2) posiblePositions.Add(startPoint + originalDirection.Rotated(-Qangle));
            }


            if (startPoint.Distance(unitPosition) < 900)
            {
                for (var i = 0; i < 3; i++)
                {
                    var pos = posiblePositions[i];
                    var direction = (pos - startPoint).Normalized().Perpendicular();
                    var k = (2/3*(unit.BoundingRadius + Q.Width));
                    posiblePositions.Add(startPoint - k*direction);
                    posiblePositions.Add(startPoint + k*direction);
                }
            }

            var bestPosition = new Vector2();
            var bestHit = -1;

            foreach (var position in posiblePositions)
            {
                var hits = CountHits(position, points, hitBoxes);
                if (hits > bestHit)
                {
                    bestPosition = position;
                    bestHit = hits;
                }
            }

            if (bestHit + 1 <= minTargets)
                return;

            Q.Cast(bestPosition.To3D(), true);
        }

        private static float ComboDamage(Obj_AI_Hero hero)
        {   
            var dmg = 0d;
            dmg += Player.GetSpellDamage(hero, SpellSlot.Q) * 2;
            dmg += Player.GetSpellDamage(hero, SpellSlot.W);
            dmg += Player.GetSpellDamage(hero, SpellSlot.Q);


            if (Items.HasItem("ItemBlackfireTorch"))
            {
                dmg += ObjectManager.Player.GetItemDamage(hero, Damage.DamageItems.Dfg);
                dmg = dmg * 1.2;
            }

            if(ObjectManager.Player.GetSpellSlot("SummonerIgnite") != SpellSlot.Unknown)
            {
                dmg += ObjectManager.Player.GetSummonerSpellDamage(hero, Damage.SummonerSpell.Ignite);
            }

            return (float) dmg;
        }

        private static void Game_OnGameUpdate(EventArgs args)
        {
            if (Config.Item("PingLH").GetValue<bool>())
                foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(h => ObjectManager.Player.Spellbook.CanUseSpell(SpellSlot.R) == SpellState.Ready && h.IsValidTarget() && ComboDamage(h) > h.Health))
                {
                    Ping(enemy.Position.To2D());
                }

            if (Config.Item("CastQ").GetValue<KeyBind>().Active)
            {
                CastQTick = Environment.TickCount;
            }

            if (Environment.TickCount - CastQTick < 500)
            {
                var qTarget = SimpleTs.GetTarget(Q.Range, SimpleTs.DamageType.Magical);
                if (qTarget != null)
                {
                    Q.Cast(qTarget);
                }
            }

            var combo = Config.Item("Combo").GetValue<KeyBind>().Active;

            //Select cards.
            if (Config.Item("SelectYellow").GetValue<KeyBind>().Active ||
                combo)
            {
                CardSelector.StartSelecting(Cards.Yellow);
            }

            if (Config.Item("SelectBlue").GetValue<KeyBind>().Active)
            {
                CardSelector.StartSelecting(Cards.Blue);
            }

            if (Config.Item("SelectRed").GetValue<KeyBind>().Active)
            {
                CardSelector.StartSelecting(Cards.Red);
            }

            if (CardSelector.Status == SelectStatus.Selected && combo)
            {
                var target = SOW.GetTarget();
                if (target.IsValidTarget() && target is Obj_AI_Hero && Items.HasItem("DeathfireGrasp") && ComboDamage((Obj_AI_Hero)target) >= target.Health)
                {
                    Items.UseItem("DeathfireGrasp", (Obj_AI_Hero) target);
                }
            }


            //Auto Q
            var autoQI = Config.Item("AutoQI").GetValue<bool>();
            var autoQD = Config.Item("AutoQD").GetValue<bool>();

            
            if (ObjectManager.Player.Spellbook.CanUseSpell(SpellSlot.Q) == SpellState.Ready && (autoQD || autoQI))
                foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>())
                {
                    if (enemy.IsValidTarget(Q.Range * 2))
                    {
                        var pred = Q.GetPrediction(enemy);
                        if ((pred.Hitchance == HitChance.Immobile && autoQI) ||
                            (pred.Hitchance == HitChance.Dashing && autoQD))
                        {
                            CastQ(enemy, pred.UnitPosition.To2D());
                        }
                    }
                }
        }
    }
}
