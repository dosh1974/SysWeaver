namespace SysWeaver.Knowledge
{
    internal static class InfoCommon
    {
        public static readonly Info TagRetro = new Info("Retro", "Anything retro (made before " + InfoConsts.RetroYear + ")", InfoConsts.GenericGroup, null);
        public static readonly Info TagVintage = new Info("Vintage", "Anything vintage (made before " + InfoConsts.VintageYear + ")", InfoConsts.GenericGroup, new[] { TagRetro } );

        static InfoCommon()
        {
            AllInfo.TryAdd(TagRetro.Name, TagRetro, false, false);
            AllInfo.TryAdd(TagVintage.Name, TagVintage, false, false);
        }

    }


}
