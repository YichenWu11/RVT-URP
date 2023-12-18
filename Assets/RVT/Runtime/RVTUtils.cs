namespace RuntimeVirtualTexture
{
    public class RVTUtils
    {
        public static void DecodePageId(uint pageId, out int mipLevel, out int pageX, out int pageY)
        {
            mipLevel = (int)(pageId >> 24);
            pageX = (int)((pageId & 0xffffff) >> 12);
            pageY = (int)(pageId & 0xfff);
        }
    }
}
