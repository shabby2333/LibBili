namespace LibBili.Danmaku
{
    public struct Danmaku
    {
        public long UserID { get; set; }
        public string UserName { get; set; }
        public string Text { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsVIP { get; set; }
        public int UserGuardLevel { get; set; }
    }
}