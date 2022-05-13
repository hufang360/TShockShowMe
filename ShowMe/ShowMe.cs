using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using TerrariaApi.Server;
using TShockAPI;

namespace ShowMe
{
    [ApiVersion(2, 1)]
    public class ShowMe : TerrariaPlugin
    {
        # region 插件信息
        public override string Name => "ShowMe";
        public override string Description => "找箱子";
        public override string Author => "hufang360";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;
        #endregion

        private string Permisson = "showme.admin";

        public ShowMe(Main game) : base(game)
        {
        }

        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command(ShowMeCMD, "showme") { HelpText = "找箱子" });
        }

        // 帮助
        private void HelpText(TSPlayer op)
        {
            op.SendInfoMessage("===== 找箱子 =====");
            op.SendInfoMessage("/showme [箱子序号], 查找快捷栏选中的物品");
            op.SendInfoMessage("/showme find <物品名|id> [箱子序号], 查找指定物品");
            if (op.HasPermission(Permisson))
            {
                op.SendInfoMessage("/showme findall <物品名|id> [箱子序号], 查找指定物品，该指令的范围是地图上的全部箱子");
            }
            op.SendInfoMessage("1.物品名还可以用 按alt+单击 物品的方式，快速完成输入；");
            op.SendInfoMessage("2.匹配到多个箱子时，可以在指令后面输入箱子序号；");
            op.SendInfoMessage("3.只查找命名过的箱子，记得给箱子命名哦；");
            op.SendInfoMessage("4.查找范围为电脑版一屏区域内；");
        }

        // 验证
        private bool Verify(TSPlayer op)
        {
            if (!op.RealPlayer)
            {
                op.SendErrorMessage("请进入游戏后，再进行操作！");
                return false;
            }

            return true;
        }

        // 快捷栏
        private void ShowMeCMD(CommandArgs args)
        {
            TSPlayer op = args.Player;


            int selectNum = 0;
            if (args.Parameters.Count >= 1)
            {
                string subcmd = args.Parameters[0].ToLowerInvariant();
                switch (subcmd)
                {
                    case "find":
                    case "f":
                        if (!Verify(op)) return;
                        args.Parameters.RemoveAt(0);
                        ShowMeWithName(args);
                        return;

                    case "findall":
                    case "fa":
                        if (!op.HasPermission(Permisson))
                        {
                            op.SendInfoMessage("你没有权限执行该指令");
                            return;
                        }
                        args.Parameters.RemoveAt(0);
                        ShowMeWithName(args, true);
                        return;

                    case "help": HelpText(op); return;

                    default:
                        bool flag = args.Parameters.Count == 1 && int.TryParse(args.Parameters[0], out selectNum);
                        if (!flag)
                        {
                            op.SendInfoMessage("语法不正确，输入 /showme help 查看帮助");
                            return;
                        }
                        break;
                }
            }

            if (!Verify(op)) return;
            Player player = Main.player[args.Player.Index];
            Item item = player.inventory[player.selectedItem];
            if (item.netID == 0 || !item.active)
                op.SendInfoMessage($"快捷栏 第 {player.selectedItem + 1} 格 没有物品！");
            else
                FindNearChest(op, item.netID.ToString(), selectNum);
        }

        // 物品名
        private void ShowMeWithName(CommandArgs args, bool superadmin = false)
        {
            TSPlayer op = args.Player;
            if (args.Parameters.Count == 0)
            {
                op.SendInfoMessage("语法不正确，输入 /showme help 查看帮助");
                return;
            }

            switch (args.Parameters[0].ToLowerInvariant())
            {
                default:
                    int selectNum = 0;
                    if (args.Parameters.Count >= 2) int.TryParse(args.Parameters[1], out selectNum);
                    FindNearChest(op, args.Parameters[0], selectNum, superadmin);
                    return;
            }
        }

        private void FindNearChest(TSPlayer op, string itemNameOrID, int selectNum = 0, bool superadmin = false)
        {
            Item item;
            if (int.TryParse(itemNameOrID, out int id))
            {
                if (id == 0)
                {
                    op.SendInfoMessage("物品名输入有误！");
                    return;
                }
                item = new Item();
                item.netDefaults(id);
            }
            else
            {
                List<Item> found = TShock.Utils.GetItemByIdOrName(itemNameOrID);
                if (found.Count == 0)
                {
                    op.SendInfoMessage("物品名输入有误！");
                    return;
                }
                else if (found.Count > 1)
                {
                    op.SendMultipleMatchError(found.Select(i => $"{i.Name}({i.netID})"));
                    return;
                }
                item = found[0];
            }

            int total = 0;
            List<Chest> chests = new List<Chest>();
            List<Point16> poss = new List<Point16>();
            Rectangle area = new Rectangle(op.TileX - 61, op.TileY - 34 + 3, 122, 68);
            foreach (Chest ch in Main.chest.Where(ch => ch != null))
            {
                if (!superadmin)
                {
                    if (string.IsNullOrEmpty(ch.name) || !InArea(area, ch.x, ch.y))
                        continue;
                }

                int stack = 0;
                foreach (Item item2 in ch.item.Where(item2 => item2 != null && item2.active && item2.netID == item.netID))
                {
                    stack += item2.stack;
                }
                if (stack == 0) continue;
                chests.Add(ch);
                poss.Add(new Point16(ch.x, ch.y));
                total += stack;
            }
            if (total == 0)
            {
                if (!superadmin)
                    op.SendInfoMessage($"附近的箱子里没有 [i:{item.netID}]{item.Name}");
                else
                    op.SendInfoMessage($"所有的箱子里都没有 [i:{item.netID}]{item.Name}");
                return;
            }

            if (selectNum <= 0)
                selectNum = 1;
            else if (selectNum > chests.Count)
                selectNum = chests.Count;

            Chest ch2 = chests[selectNum - 1];
            if (op.RealPlayer) op.Teleport(ch2.x * 16, (ch2.y - 2) * 16);
            if (!superadmin) op.SendInfoMessage($"附近 有{chests.Count}个箱子 存放了 [i:{item.netID}]{item.Name}，共计{total}件");
            else
            {
                if (op.RealPlayer)
                    op.SendInfoMessage($"查找全图发现 有{chests.Count}个箱子 存放了 [i:{item.netID}]{item.Name}，共计{total}件");
                else
                    op.SendInfoMessage($"在这些箱子里找到了物品({poss.Count})：\n{string.Join(", ", poss)}");
            }
        }

        private bool InArea(Rectangle rect, int x, int y)
        {
            return x >= rect.X && x <= rect.X + rect.Width && y >= rect.Y && y <= rect.Y + rect.Height;
        }

    }

}
