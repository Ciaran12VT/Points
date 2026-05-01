using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Points.Views.Cards
{
    public sealed class ReportCardTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? ReportTemplate { get; set; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        {
            // For now, all items are ReportModel ? one template
            return ReportTemplate
                   ?? throw new InvalidOperationException(
                       "ReportTemplate must be set on ReportCardTemplateSelector");
        }
    }
}
