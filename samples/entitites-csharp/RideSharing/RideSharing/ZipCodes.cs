using System;
using System.Collections.Generic;
using System.Text;

namespace RideSharing
{
    public static class ZipCodes
    {
        // list of zipcodes. Not real, it's just a sample.
        public static List<int> All = new List<int> {
                98101, 98102, 98104, 98105, 98108, 98109, 98112, 98113, 98114, 98117, 98103, 98106, 98107,
                98111, 98115, 98116, 98118, 98119, 98121, 98125, 98126, 98132, 98133, 98138, 98139, 98141,
                98122, 98124, 98127, 98129, 98131, 98134, 98136, 98144, 98145, 98148, 98155, 98160, 98161,
                98164, 98165, 98168, 98170, 98146, 98154, 98158, 98166, 98174, 98175, 98178, 98190, 98191,
                98177, 98181, 98185, 98188, 98189, 98194, 98195, 98199, 98198
        };


        // Find nearby zipcodes. Not real, it's just a sample.
        public static IEnumerable<int> GetProximityList(int zipCode)
        {
            var position = All.IndexOf(zipCode);

            yield return All[position];
            if (position + 1 < All.Count) yield return All[position + 1];
            if (position - 1 > 0) yield return All[position - 1];
            if (position + 2 < All.Count) yield return All[position + 2];
            if (position - 2 > 0) yield return All[position - 2];
        }
    }
}
