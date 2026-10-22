namespace ShootingGallery
{
    /// <summary>
    /// GalleryBuilder のうち、GameManager が鳴らすモードの BGM (夏祭り・幽霊・怪獣・鉱山・海戦・西部劇・宇宙・潜水艦・忍者・アンノウン)。
    /// エディターでは Assets/Source から、実機では GalleryRuntimeAssets から読む
    /// </summary>
    public partial class GalleryBuilder
    {
        public const string FestivalBgmPath = "Assets/Source/festival_stage/maou_bgm_ethnic09.mp3"; // 夏祭りモード (魔王魂)
        public const string GhostBgmPath = "Assets/Source/ghost_stage/Noisy_Grave.mp3";              // 幽霊モード
        public const string KaijuBgmPath = "Assets/Source/kaiju_stage/Candy_Coated_Catastrophe.mp3"; // 怪獣モード
        public const string MineBgmPath = "Assets/Source/mine_stage/Veins_of_Glowing_Stone.mp3";     // 鉱山モード
        public const string NavalBgmPath = "Assets/Source/pirate_stage/Battle_on_the_Ship.mp3";      // 海戦モード
        public const string WesternBgmPath = "Assets/Source/western_stage/monume-american-western-country-576992.mp3"; // 西部劇モード
        public const string SpaceBgmPath = "Assets/Source/space_stage/leberch-space-587944.mp3"; // 宇宙モード (星空が見えてきた頃から)
        public const string SubmarineBgmPath = "Assets/Source/ocean_stage/kallydru-ocean-stillness-407237.mp3"; // 潜水艦モード
        public const string NinjaBgmPath = "Assets/Source/ninja_stage/kazekiri.mp3"; // 忍者モード (正面のふすまが開いた時から)
        public const string UnknownBgmPath = "Assets/Source/unknown_stage/lux-aeterna-liminal-pigments-612888.mp3"; // アンノウンモード
        public const string RoundBgmPath = "Assets/Source/normal/offenbach_tengokutojigoku.mp3"; // ラウンドの BGM (2 MIN / 100,000 POINTS / エンドレス)
        public const string RevolverShotPath = "Assets/Source/western_stage/gun/mrfriends-pistol-shot-233473.mp3";    // 西部劇のリボルバーの銃声
        public const string RevolverReloadPath = "Assets/Source/western_stage/gun/universfield-gun-reload-123781.mp3"; // 西部劇の弾を込める音

        /// <summary>BGM の読み込み方を軽くする曲 (Editor/ConcertBgmImport)</summary>
        public static readonly string[] StageBgmPaths = { FestivalBgmPath, GhostBgmPath, KaijuBgmPath, MineBgmPath, NavalBgmPath, WesternBgmPath, SpaceBgmPath, SubmarineBgmPath, NinjaBgmPath, RoundBgmPath, UnknownBgmPath, FinaleApplausePath };

        void AssignStageBgm(GameManager gm)
        {
            gm.festivalBgm = LoadSourceAsset(FestivalBgmPath, a => a.festivalBgm);
            gm.ghostBgm = LoadSourceAsset(GhostBgmPath, a => a.ghostBgm);
            gm.kaijuBgm = LoadSourceAsset(KaijuBgmPath, a => a.kaijuBgm);
            gm.mineBgm = LoadSourceAsset(MineBgmPath, a => a.mineBgm);
            gm.navalBgm = LoadSourceAsset(NavalBgmPath, a => a.navalBgm);
            gm.westernBgm = LoadSourceAsset(WesternBgmPath, a => a.westernBgm);
            gm.spaceBgm = LoadSourceAsset(SpaceBgmPath, a => a.spaceBgm);
            gm.submarineBgm = LoadSourceAsset(SubmarineBgmPath, a => a.submarineBgm);
            gm.ninjaBgm = LoadSourceAsset(NinjaBgmPath, a => a.ninjaBgm);
            gm.unknownBgm = LoadSourceAsset(UnknownBgmPath, a => a.unknownBgm);
            gm.roundBgm = LoadSourceAsset(RoundBgmPath, a => a.roundBgm);
            gm.finaleApplause = LoadSourceAsset(FinaleApplausePath, a => a.finaleApplause);
        }
    }
}
