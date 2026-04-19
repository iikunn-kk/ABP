using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class ABHashUtil
{
    public static string ComputeMD5File(string fullPath)
    {
        using (MD5 md5 = MD5.Create())
        using (FileStream stream = File.OpenRead(fullPath))
        {
            byte[] hash = md5.ComputeHash(stream);
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++)
                sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
