using System.Linq;

namespace Tanneryd.BulkOperations.EF6.NetStd.Model
{
    public class KeyPropertyMapping
    {
        public string ItemPropertyName { get; set; }
        public string EntityPropertyName { get; set; }

        public static KeyPropertyMapping[] IdentityMappings(string[] names)
        {
            return names.Select(n => new KeyPropertyMapping
            {
                ItemPropertyName = n,
                EntityPropertyName = n
            }).ToArray();
        }
    }
}