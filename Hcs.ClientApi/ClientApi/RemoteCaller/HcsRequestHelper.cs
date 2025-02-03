
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hcs.ClientApi.RemoteCaller
{
    public static class HcsRequestHelper
    {
        /// <summary>
        /// Подготовка заголовка сообщения отправляемого в ГИС ЖКХ с обязательными атрибутами.
        /// Заголовки могут быть разного типа для разных типов сообщений но имена полей одинаковые.
        /// </summary>
        public static THeaderType CreateHeader<THeaderType>(HcsClientConfig config) where THeaderType : class
        {
            try {
                var instance = Activator.CreateInstance(typeof(THeaderType));

                foreach (var prop in instance.GetType().GetProperties()) {
                    switch (prop.Name) {
                        case "Item":
                            prop.SetValue(instance, config.OrgPPAGUID);
                            break;
                        case "ItemElementName":
                            prop.SetValue(instance, Enum.Parse(prop.PropertyType, "orgPPAGUID"));
                            break;
                        case "MessageGUID":
                            prop.SetValue(instance, Guid.NewGuid().ToString());
                            break;
                        case "Date":
                            prop.SetValue(instance, DateTime.Now);
                            break;
                        case "IsOperatorSignatureSpecified":
                            if (config.Role == HcsOrganizationRoles.RC || config.Role == HcsOrganizationRoles.RSO)
                                prop.SetValue(instance, true);
                            break;
                        case "IsOperatorSignature":
                            if (config.Role == HcsOrganizationRoles.RC || config.Role == HcsOrganizationRoles.RSO)
                                prop.SetValue(instance, true);
                            break;
                    }
                }

                return instance as THeaderType;
            }
            catch (ArgumentNullException ex) {
                throw new ApplicationException($"При сборке заголовка запроса для ГИС произошла ошибка: {ex.Message}");
            }
            catch (SystemException exc) {
                throw new ApplicationException($"При сборке заголовка запроса для ГИС произошла не предвиденная ошибка {exc.GetBaseException().Message}");
            }
        }

        /// <summary>
        /// Для объекта запроса возвращает значение строки свойства version.
        /// </summary>
        public static string GetRequestVersionString(object requestObject)
        {
            if (requestObject == null) return null;
            object versionHost = requestObject;

            if (versionHost != null) { 
                var versionProperty = versionHost.GetType().GetProperties().FirstOrDefault(x => x.Name == "version");
                if (versionProperty != null) return versionProperty.GetValue(versionHost) as string;
            }

            foreach (var field in requestObject.GetType().GetFields()) {
                versionHost = field.GetValue(requestObject);
                if (versionHost != null) {
                    var versionProperty = versionHost.GetType().GetProperties().FirstOrDefault(x => x.Name == "version");
                    if (versionProperty != null) return versionProperty.GetValue(versionHost) as string;
                }
            }

            return null;
        }
    }
}
