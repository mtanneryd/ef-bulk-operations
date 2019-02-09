using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tanneryd.BulkOperations.EF6.Tests
{
    [TestClass]
    public class ListExtensionTests
    {
        [TestMethod]
        public void UntypedListShouldBeConvertedToTypedList()
        {
           IList listOfStrings = new List<dynamic> { "element1","element2","element3"};
           Assert.IsFalse(listOfStrings is IList<string>);
            var convertedListOfStrings = listOfStrings.ToArray(typeof(string));
           Assert.IsTrue(convertedListOfStrings is IList<string>);
        }
    }
}