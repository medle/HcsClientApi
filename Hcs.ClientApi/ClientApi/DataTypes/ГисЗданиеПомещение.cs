using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi.DataTypes
{
    public record struct ГисЗданиеПомещение(ГисЗдание Здание, ГисПомещение Помещение)
    { 
        public bool Пустое => (Здание == null || Помещение == null);
        public bool Заполнено => !Пустое;
    }
}
