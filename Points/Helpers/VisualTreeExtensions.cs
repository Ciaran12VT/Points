using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Helpers
{
    public static class VisualTreeExtensions
    {
        public static T? FindParentOfType<T>(this Element element) where T : Element
        {
            Element? current = element;
            while (current != null)
            {
                if (current is T t) return t;
                current = current.Parent;
            }
            return null;
        }

        public static IEnumerable<T> FindDescendants<T>(this Element element) where T : Element
        {
            if (element is T t)
                yield return t;

            foreach (var child in element.LogicalChildren)
            {
                if (child is Element e)
                {
                    foreach (var match in e.FindDescendants<T>())
                        yield return match;
                }
            }
        }

    }
}
