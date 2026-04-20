using TaleWorlds.Library;

namespace Enlisted.Features.Equipment.UI
{
    /// <summary>
    /// Row container for equipment cards displayed in a grid layout.
    /// 
    /// Contains a list of equipment item ViewModels that are displayed together in a row.
    /// Used by ListPanel with ItemTemplate in the main UI for grid-based equipment selection.
    /// </summary>
    public class QuartermasterEquipmentRowVm : ViewModel
    {
        [DataSourceProperty]
        public MBBindingList<QuartermasterEquipmentItemVm> Cards { get; private set; }

        /// <summary>
        /// Initialize equipment row with a list of equipment card ViewModels.
        /// </summary>
        public QuartermasterEquipmentRowVm(MBBindingList<QuartermasterEquipmentItemVm> cards)
        {
            Cards = cards;
        }
    }
}
