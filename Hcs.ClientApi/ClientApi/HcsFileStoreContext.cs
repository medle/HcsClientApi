using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi
{
    /// <summary>
    /// Раздел хранилища файлов (Attachment) из документации: ГИС ЖКХ. Альбом ТФФ 14.5.0.1.docx
    /// 2.6	Перечень контекстов хранилищ функциональных подсистем.
    /// </summary>
    public enum HcsFileStoreContext
    {
        /// <summary>
        /// Управление домами. Лицевые счета.
        /// </summary>
        homemanagement,

        /// <summary>
        /// Управление контентом
        /// </summary>
        contentmanagement,

        /// <summary>
        /// Электронные счета
        /// </summary>
        bills,

        /// <summary>
        /// Запросов о наличии задолженности по оплате ЖКУ
        /// </summary>
        debtreq
    }

    public static class HcsFileStoreContextExtensions
    {
        public static string GetName(this HcsFileStoreContext context)
        {
            return context.ToString();
        }
    }
}
