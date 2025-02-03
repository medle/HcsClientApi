
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi.RemoteCaller
{
    /// <summary>
    /// Состояние многостраничной выдачи для методов HCS выдыющих длинные списки.
    /// Списки выдаются порциями по 100 позиций и в каждой порции указано состояние
    /// многостраничной выдачи одним значением - это либо bool со значением true что
    /// означает что эта порция последняя IsLastPage, либо это строка содержащая 
    /// guid объекта начала следующей порции - и этот guid надо указать в запросе
    /// чтобы получить следующую порцию.
    /// </summary>
    public class HcsPagedResultState
    {
        /// <summary>
        /// Состояние указыввает что это последняя страница.
        /// </summary>
        public bool IsLastPage { get; private set; }

        /// <summary>
        /// Состояние указывает что это не последняя страница и 
        /// следующая страница начинается с NextGuid.
        /// </summary>
        public Guid NextGuid { get; private set; }

        private const string me = nameof(HcsPagedResultState);

        public static readonly HcsPagedResultState IsLastPageResultState = new HcsPagedResultState(true);

        /// <summary>
        /// Новый маркер состояния многостраничной выдачи метода HCS.
        /// </summary>
        public HcsPagedResultState(object item)
        {
            if (item == null) throw new HcsException($"{me}.Item is null");

            if (item is bool) {
                if ((bool)item == false) throw new HcsException($"{me}.IsLastPage is false");
                IsLastPage = true;
            }
            else if (item is string) {
                try {
                    IsLastPage = false;
                    NextGuid = HcsUtil.ParseGuid((string)item);
                }
                catch (Exception e) {
                    throw new HcsException($"Failed to parse {me}.NextGuid value", e);
                }
            }
            else {
                throw new HcsException($"{me}.Item is of unrecognized type " + item.GetType().FullName);
            }
        }

        public override string ToString()
        {
            return $"{me}({nameof(IsLastPage)}={IsLastPage}" + 
                (IsLastPage ? "" : $",{nameof(NextGuid)}={NextGuid}") + ")";
        }
    }
}
