using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using RoundsClone.Core.Common;
using RoundsClone.Core.Graphics;
using RoundsClone.Core.Physics;

namespace RoundsClone.Core.Maps;

/// <summary>
/// 纯程序化生成的战斗地图场景数据。
/// </summary>
public class MapData
{
    public string Id { get; set; } = "map_default";
    public string Name { get; set; } = "经典竞技场";
    public string Description { get; set; } = "平衡对称的对抗地图，拥有中央浮岛与侧翼掩体。";
    public string EnvironmentalModifier { get; set; } = "标准物理环境";

    public Vector2 PlayerSpawn { get; set; } = new(350f, 750f);
    public Vector2 AiSpawn { get; set; } = new(1570f, 750f);

    public List<Rectangle> SolidWalls { get; } = new();
    public List<Rectangle> BouncyPillars { get; } = new();

    private readonly List<BoxCollider> _registeredColliders = new();

    /// <summary>
    /// 将地图的静态实体边界构建并注入物理世界。
    /// </summary>
    public void BuildPhysicsWorld(CollisionWorld world)
    {
        _registeredColliders.Clear();

        // 注入普通墙体
        foreach (var rect in SolidWalls)
        {
            var body = new RigidBody2D(new Vector2(rect.Center.X, rect.Center.Y), 0f)
            {
                Restitution = 0.2f
            };
            var collider = new BoxCollider(body, new Vector2(rect.Width, rect.Height))
            {
                BouncinessOverride = 0.2f
            };
            world.AddCollider(collider);
            _registeredColliders.Add(collider);
        }

        // 注入高弹性特殊反弹柱
        foreach (var rect in BouncyPillars)
        {
            var body = new RigidBody2D(new Vector2(rect.Center.X, rect.Center.Y), 0f)
            {
                Restitution = 1.0f
            };
            var collider = new BoxCollider(body, new Vector2(rect.Width, rect.Height))
            {
                BouncinessOverride = 1.0f
            };
            world.AddCollider(collider);
            _registeredColliders.Add(collider);
        }
    }

    /// <summary>
    /// 从物理世界中注销本关卡碰撞体。
    /// </summary>
    public void CleanPhysicsWorld(CollisionWorld world)
    {
        foreach (var col in _registeredColliders)
        {
            world.RemoveCollider(col);
        }
        _registeredColliders.Clear();
    }

    /// <summary>
    /// 渲染程序化地图几何背景与墙体。
    /// </summary>
    public void Draw(ShapeBatcher batcher, SpriteBatch spriteBatch)
    {
        // 1. 渲染背景科技网格
        for (int x = 0; x < VirtualViewport.VirtualWidth; x += 120)
        {
            batcher.DrawLine(spriteBatch, new Vector2(x, 0), new Vector2(x, VirtualViewport.VirtualHeight), ColorPalette.GridLine, 1f);
        }
        for (int y = 0; y < VirtualViewport.VirtualHeight; y += 120)
        {
            batcher.DrawLine(spriteBatch, new Vector2(0, y), new Vector2(VirtualViewport.VirtualWidth, y), ColorPalette.GridLine, 1f);
        }

        // 2. 渲染实体墙壁
        foreach (var wall in SolidWalls)
        {
            batcher.DrawRoundedRect(spriteBatch, wall, 4f, ColorPalette.WallSolid);
            batcher.DrawRectangleOutline(spriteBatch, wall, ColorPalette.WallOutline, 2.5f);
        }

        // 3. 渲染高弹性粉红反弹柱
        foreach (var bounce in BouncyPillars)
        {
            batcher.DrawRoundedRect(spriteBatch, bounce, 6f, ColorPalette.BouncySurface);
            batcher.DrawRectangleOutline(spriteBatch, bounce, Color.White, 3f);
        }
    }

    /// <summary>
    /// 绘制缩略图（用于关卡三选一界面中的卡片预览）。
    /// </summary>
    public void DrawThumbnail(ShapeBatcher batcher, SpriteBatch spriteBatch, Rectangle destRect)
    {
        float scaleX = (float)destRect.Width / VirtualViewport.VirtualWidth;
        float scaleY = (float)destRect.Height / VirtualViewport.VirtualHeight;

        batcher.DrawRectangle(spriteBatch, destRect, ColorPalette.BackgroundDark);
        batcher.DrawRectangleOutline(spriteBatch, destRect, ColorPalette.WallOutline, 2f);

        foreach (var wall in SolidWalls)
        {
            Rectangle thumbWall = new(
                destRect.X + (int)(wall.X * scaleX),
                destRect.Y + (int)(wall.Y * scaleY),
                Math.Max(2, (int)(wall.Width * scaleX)),
                Math.Max(2, (int)(wall.Height * scaleY))
            );
            batcher.DrawRectangle(spriteBatch, thumbWall, ColorPalette.WallSolid);
        }

        foreach (var bounce in BouncyPillars)
        {
            Rectangle thumbBounce = new(
                destRect.X + (int)(bounce.X * scaleX),
                destRect.Y + (int)(bounce.Y * scaleY),
                Math.Max(2, (int)(bounce.Width * scaleX)),
                Math.Max(2, (int)(bounce.Height * scaleY))
            );
            batcher.DrawRectangle(spriteBatch, thumbBounce, ColorPalette.BouncySurface);
        }
    }
}

/// <summary>
/// 地图库与程序化竞技场生成工厂。
/// </summary>
public static class MapRegistry
{
    /// <summary>
    /// 生成包含 5 种不同战术风格的完整竞技场集合。
    /// </summary>
    public static List<MapData> CreateAllMaps()
    {
        return new List<MapData>
        {
            CreateClassicArena(),
            CreateBumperMaze(),
            CreateFloatingIslands(),
            CreateBunkerCatwalks(),
            CreateVerticalFactory()
        };
    }

    /// <summary>
    /// 地图 1: 经典高密对称竞技场
    /// </summary>
    public static MapData CreateClassicArena()
    {
        var map = new MapData
        {
            Id = "map_classic",
            Name = "经典对称竞技场",
            Description = "多层对称台阶与中央战斗堡垒，具有立体掩体与攀爬天井。",
            EnvironmentalModifier = "标准力学环境",
            PlayerSpawn = new(280f, 960f),
            AiSpawn = new(1640f, 960f)
        };

        AddBorders(map);

        // 1. 底层侧翼起跳台阶
        map.SolidWalls.Add(new Rectangle(120, 880, 240, 32));
        map.SolidWalls.Add(new Rectangle(1560, 880, 240, 32));
        map.SolidWalls.Add(new Rectangle(340, 770, 200, 32));
        map.SolidWalls.Add(new Rectangle(1380, 770, 200, 32));

        // 2. 中层两翼平台与防弹立柱掩体
        map.SolidWalls.Add(new Rectangle(160, 600, 280, 32));
        map.SolidWalls.Add(new Rectangle(1480, 600, 280, 32));
        map.SolidWalls.Add(new Rectangle(400, 480, 40, 120));   // 左翼掩体立柱
        map.SolidWalls.Add(new Rectangle(1480, 480, 40, 120));  // 右翼掩体立柱

        // 3. 高层悬浮翼台
        map.SolidWalls.Add(new Rectangle(440, 390, 220, 32));
        map.SolidWalls.Add(new Rectangle(1260, 390, 220, 32));

        // 4. 中央多层战斗堡垒与双侧攀爬竖井 (竖井宽180px，支持连续蹬墙跳)
        map.SolidWalls.Add(new Rectangle(740, 800, 440, 32));   // 中央底层桥梁
        map.SolidWalls.Add(new Rectangle(710, 580, 40, 250));   // 堡垒左立柱
        map.SolidWalls.Add(new Rectangle(1170, 580, 40, 250));  // 堡垒右立柱
        map.SolidWalls.Add(new Rectangle(660, 580, 600, 32));   // 中央主战平台
        map.SolidWalls.Add(new Rectangle(920, 430, 80, 150));   // 中央分火掩体方块
        map.SolidWalls.Add(new Rectangle(780, 380, 360, 32));   // 顶层狙击连廊

        // 5. 反弹垫与动能弹射器
        map.BouncyPillars.Add(new Rectangle(890, 1000, 140, 30)); // 地面中央发射蹦床
        map.BouncyPillars.Add(new Rectangle(885, 230, 150, 36));  // 顶部天花板折射板
        map.BouncyPillars.Add(new Rectangle(50, 420, 35, 180));   // 左外侧墙面反弹板
        map.BouncyPillars.Add(new Rectangle(1835, 420, 35, 180)); // 右外侧墙面反弹板

        return map;
    }

    /// <summary>
    /// 地图 2: 高能反弹迷宫
    /// </summary>
    public static MapData CreateBumperMaze()
    {
        var map = new MapData
        {
            Id = "map_bumper",
            Name = "高能反弹迷宫",
            Description = "错落交织的锯齿状平台，布满高弹力缓冲墙与反弹蹦床。",
            EnvironmentalModifier = "全场高弹性反弹",
            PlayerSpawn = new(240f, 960f),
            AiSpawn = new(1680f, 960f)
        };

        AddBorders(map);

        // 1. 底层与中层错位台阶 (Z字形攀爬动线)
        map.SolidWalls.Add(new Rectangle(140, 870, 240, 32));
        map.SolidWalls.Add(new Rectangle(1540, 870, 240, 32));
        map.SolidWalls.Add(new Rectangle(380, 750, 240, 32));
        map.SolidWalls.Add(new Rectangle(1300, 750, 240, 32));
        map.SolidWalls.Add(new Rectangle(160, 610, 260, 32));
        map.SolidWalls.Add(new Rectangle(1500, 610, 260, 32));
        map.SolidWalls.Add(new Rectangle(420, 480, 240, 32));
        map.SolidWalls.Add(new Rectangle(1260, 480, 240, 32));
        map.SolidWalls.Add(new Rectangle(200, 340, 220, 32));
        map.SolidWalls.Add(new Rectangle(1500, 340, 220, 32));

        // 2. 中央战区多层桥梁与掩体柱
        map.SolidWalls.Add(new Rectangle(760, 730, 400, 32));
        map.SolidWalls.Add(new Rectangle(700, 520, 520, 32));
        map.SolidWalls.Add(new Rectangle(820, 320, 280, 32));
        map.SolidWalls.Add(new Rectangle(935, 352, 50, 168));   // 中央垂立阻隔柱

        // 3. 高能弹性反弹构件
        map.BouncyPillars.Add(new Rectangle(660, 995, 200, 35)); // 地面左蹦床
        map.BouncyPillars.Add(new Rectangle(1060, 995, 200, 35));// 地面右蹦床
        map.BouncyPillars.Add(new Rectangle(560, 360, 40, 240)); // 左垂直反弹柱
        map.BouncyPillars.Add(new Rectangle(1320, 360, 40, 240));// 右垂直反弹柱
        map.BouncyPillars.Add(new Rectangle(860, 180, 200, 36));  // 顶层悬空反弹块
        map.BouncyPillars.Add(new Rectangle(50, 540, 35, 160));  // 侧翼反弹贴片
        map.BouncyPillars.Add(new Rectangle(1835, 540, 35, 160));

        return map;
    }

    /// <summary>
    /// 地图 3: 深渊浮空岛群
    /// </summary>
    public static MapData CreateFloatingIslands()
    {
        var map = new MapData
        {
            Id = "map_islands",
            Name = "深渊浮空岛",
            Description = "多层天空浮岛群与深渊虚空，底层救生蹦床带来极速回弹。",
            EnvironmentalModifier = "高空虚空强风",
            PlayerSpawn = new(280f, 760f),
            AiSpawn = new(1640f, 760f)
        };

        AddBorders(map);

        // 1. 底层多块浮空岛屿
        map.SolidWalls.Add(new Rectangle(140, 810, 280, 36));
        map.SolidWalls.Add(new Rectangle(1500, 810, 280, 36));
        map.SolidWalls.Add(new Rectangle(490, 840, 240, 36));
        map.SolidWalls.Add(new Rectangle(1190, 840, 240, 36));
        map.SolidWalls.Add(new Rectangle(820, 870, 280, 36));

        // 2. 中层连环浮空岛
        map.SolidWalls.Add(new Rectangle(260, 650, 260, 36));
        map.SolidWalls.Add(new Rectangle(1400, 650, 260, 36));
        map.SolidWalls.Add(new Rectangle(620, 620, 280, 36));
        map.SolidWalls.Add(new Rectangle(1020, 620, 280, 36));

        // 3. 高层圣殿与空岛石柱
        map.SolidWalls.Add(new Rectangle(400, 460, 220, 36));
        map.SolidWalls.Add(new Rectangle(1300, 460, 220, 36));
        map.SolidWalls.Add(new Rectangle(720, 430, 480, 36));   // 天空圣殿主平台
        map.SolidWalls.Add(new Rectangle(810, 260, 300, 36));   // 圣殿顶层阁楼
        map.SolidWalls.Add(new Rectangle(740, 296, 36, 134));   // 圣殿左立柱
        map.SolidWalls.Add(new Rectangle(1144, 296, 36, 134));  // 圣殿右立柱
        map.SolidWalls.Add(new Rectangle(140, 420, 160, 32));   // 左边缘高空哨所
        map.SolidWalls.Add(new Rectangle(1620, 420, 160, 32));  // 右边缘高空哨所

        // 4. 深渊防跌落反弹蹦床与空中折射点
        map.BouncyPillars.Add(new Rectangle(320, 995, 240, 35)); // 深渊左救生垫
        map.BouncyPillars.Add(new Rectangle(1360, 995, 240, 35));// 深渊右救生垫
        map.BouncyPillars.Add(new Rectangle(840, 995, 240, 35)); // 深渊中救生垫
        map.BouncyPillars.Add(new Rectangle(50, 280, 35, 160));  // 高空侧翼回弹
        map.BouncyPillars.Add(new Rectangle(1835, 280, 35, 160));

        return map;
    }

    /// <summary>
    /// 地图 4: 紧凑地下掩体
    /// </summary>
    public static MapData CreateBunkerCatwalks()
    {
        var map = new MapData
        {
            Id = "map_bunker",
            Name = "紧凑掩体死斗场",
            Description = "厚重混凝土廊道与防爆立柱，极度考验近战缠斗与走位博弈。",
            EnvironmentalModifier = "封闭空间回音",
            PlayerSpawn = new(240f, 960f),
            AiSpawn = new(1680f, 960f)
        };

        AddBorders(map);

        // 1. 底层掩体顶板与中央主防爆墙
        map.SolidWalls.Add(new Rectangle(120, 830, 380, 36));
        map.SolidWalls.Add(new Rectangle(1420, 830, 380, 36));
        map.SolidWalls.Add(new Rectangle(930, 750, 60, 280));   // 中央主防爆柱
        map.SolidWalls.Add(new Rectangle(420, 730, 40, 100));   // 左低位射击护栏
        map.SolidWalls.Add(new Rectangle(1460, 730, 40, 100));  // 右低位射击护栏

        // 2. 中层工业走廊与攀爬井
        map.SolidWalls.Add(new Rectangle(220, 630, 320, 32));
        map.SolidWalls.Add(new Rectangle(1380, 630, 320, 32));
        map.SolidWalls.Add(new Rectangle(620, 640, 240, 32));
        map.SolidWalls.Add(new Rectangle(1060, 640, 240, 32));
        map.SolidWalls.Add(new Rectangle(620, 440, 36, 200));   // 攀爬天井壁 A
        map.SolidWalls.Add(new Rectangle(1264, 440, 36, 200));  // 攀爬天井壁 B

        // 3. 高层指挥桥与狙击据点
        map.SolidWalls.Add(new Rectangle(360, 440, 240, 32));
        map.SolidWalls.Add(new Rectangle(1320, 440, 240, 32));
        map.SolidWalls.Add(new Rectangle(740, 440, 440, 36));   // 中央主天桥
        map.SolidWalls.Add(new Rectangle(840, 280, 240, 32));   // 顶层观察塔
        map.SolidWalls.Add(new Rectangle(840, 140, 40, 140));   // 塔顶排气立柱
        map.SolidWalls.Add(new Rectangle(1040, 140, 40, 140));

        // 4. 掩体应急弹射与折射装置
        map.BouncyPillars.Add(new Rectangle(920, 570, 80, 32));   // 中井垂直强力弹射板
        map.BouncyPillars.Add(new Rectangle(480, 160, 160, 32));  // 顶部折射板 L
        map.BouncyPillars.Add(new Rectangle(1280, 160, 160, 32)); // 顶部折射板 R
        map.BouncyPillars.Add(new Rectangle(50, 700, 35, 150));   // 侧门推斥器
        map.BouncyPillars.Add(new Rectangle(1835, 700, 35, 150));

        return map;
    }

    /// <summary>
    /// 地图 5: 立体工业塔
    /// </summary>
    public static MapData CreateVerticalFactory()
    {
        var map = new MapData
        {
            Id = "map_factory",
            Name = "立体工业塔",
            Description = "拥有双垂直电梯竖井与高空行车横梁，展现极高纵深作战能力。",
            EnvironmentalModifier = "垂直力场",
            PlayerSpawn = new(240f, 960f),
            AiSpawn = new(1680f, 960f)
        };

        AddBorders(map);

        // 1. 底层物流踏板
        map.SolidWalls.Add(new Rectangle(120, 860, 240, 36));
        map.SolidWalls.Add(new Rectangle(1560, 860, 240, 36));
        map.SolidWalls.Add(new Rectangle(300, 750, 160, 36));
        map.SolidWalls.Add(new Rectangle(1460, 750, 160, 36));

        // 2. 左右双垂直电梯竖井 (竖井间距 160px，极佳的蹬墙跳连续上升通道)
        // 左竖井
        map.SolidWalls.Add(new Rectangle(460, 360, 40, 500));
        map.SolidWalls.Add(new Rectangle(640, 360, 40, 500));
        map.SolidWalls.Add(new Rectangle(500, 600, 140, 26));  // 井内中继歇脚点
        // 右竖井
        map.SolidWalls.Add(new Rectangle(1240, 360, 40, 500));
        map.SolidWalls.Add(new Rectangle(1420, 360, 40, 500));
        map.SolidWalls.Add(new Rectangle(1280, 600, 140, 26)); // 井内中继歇脚点

        // 3. 中央车间生产线与高空桁架
        map.SolidWalls.Add(new Rectangle(740, 780, 440, 32));  // 输送带天桥
        map.SolidWalls.Add(new Rectangle(680, 580, 560, 32));  // 主检修走道
        map.SolidWalls.Add(new Rectangle(760, 380, 400, 32));  // 控制中枢平层
        map.SolidWalls.Add(new Rectangle(640, 200, 640, 36));  // 顶层起重天车大梁
        map.SolidWalls.Add(new Rectangle(930, 236, 60, 80));   // 吊装行车方块掩体

        // 4. 外侧高位停机坪
        map.SolidWalls.Add(new Rectangle(140, 480, 220, 32));
        map.SolidWalls.Add(new Rectangle(1560, 480, 220, 32));
        map.SolidWalls.Add(new Rectangle(180, 280, 180, 32));
        map.SolidWalls.Add(new Rectangle(1560, 280, 180, 32));

        // 5. 工业排气蹦床与升降反弹垫
        map.BouncyPillars.Add(new Rectangle(860, 995, 200, 35));  // 地面高压蒸汽弹跳口
        map.BouncyPillars.Add(new Rectangle(885, 480, 150, 32));  // 中空反弹板
        map.BouncyPillars.Add(new Rectangle(500, 820, 140, 28));  // 左竖井底反弹加速块
        map.BouncyPillars.Add(new Rectangle(1280, 820, 140, 28)); // 右竖井底反弹加速块

        return map;
    }

    private static void AddBorders(MapData map)
    {
        // 游戏外围边界 (1920x1080)
        int thickness = 50;
        int w = VirtualViewport.VirtualWidth;
        int h = VirtualViewport.VirtualHeight;

        map.SolidWalls.Add(new Rectangle(0, 0, w, thickness));               // 顶部天花板
        map.SolidWalls.Add(new Rectangle(0, h - thickness, w, thickness));   // 底部地面
        map.SolidWalls.Add(new Rectangle(0, 0, thickness, h));               // 左侧墙壁
        map.SolidWalls.Add(new Rectangle(w - thickness, 0, thickness, h));   // 右侧墙壁
    }

    /// <summary>
    /// 随机抽取 3 张不同关卡卡牌（用于关卡三选一界面）。
    /// </summary>
    public static List<MapData> DraftMaps(int count = 3)
    {
        var all = CreateAllMaps();
        var rand = new Random();
        for (int i = all.Count - 1; i > 0; i--)
        {
            int swap = rand.Next(i + 1);
            (all[i], all[swap]) = (all[swap], all[i]);
        }
        return all.Take(Math.Min(count, all.Count)).ToList();
    }
}
