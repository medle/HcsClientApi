using System.Collections.Generic;

namespace Hcs.ClientApi.Config
{
    public static class HcsConstants
    {
        public const string SignElementId = "signed-data-container";

        /// <summary>
        /// Если PIN сертификата не указан пользователем, применяется это значение
        /// по умолчанию для сертификатов RuToken.
        /// </summary>
        public const string DefaultCertificatePin = "12345678";

        /// <summary>
        /// Версия классов Альбома ТФФ ГИС ЖКХ по умолчанию.
        /// </summary>
        public const string DefaultHCSVersionString = "13.1.10.1";

        public static class Address
        {
            public const string UriPPAK = "api.dom.gosuslugi.ru";
            public const string UriSIT = "217.107.108.147:10081"; //SIT1
            public const string UriSIT2 = "217.107.108.156:10081"; //SIT2
            public const string UriTunnel = "127.0.0.1:8080";
        }

        public static class EndPointLocator
        {
            static Dictionary<HcsEndPoints, string> _endPoints;
            static EndPointLocator()
            {
                if (_endPoints == null)
                    _endPoints = new Dictionary<HcsEndPoints, string>();

                _endPoints.Add(HcsEndPoints.BillsAsync, "ext-bus-bills-service/services/BillsAsync");
                _endPoints.Add(HcsEndPoints.DeviceMetering, "ext-bus-device-metering-service/services/DeviceMetering");
                _endPoints.Add(HcsEndPoints.DeviceMeteringAsync, "ext-bus-device-metering-service/services/DeviceMeteringAsync");
                _endPoints.Add(HcsEndPoints.HouseManagement, "ext-bus-home-management-service/services/HomeManagement");
                _endPoints.Add(HcsEndPoints.HouseManagementAsync, "ext-bus-home-management-service/services/HomeManagementAsync");
                _endPoints.Add(HcsEndPoints.DebtRequestsAsync, "ext-bus-debtreq-service/services/DebtRequestsAsync");
                _endPoints.Add(HcsEndPoints.Licenses, "ext-bus-licenses-service/services/Licenses");
                _endPoints.Add(HcsEndPoints.LicensesAsync, "ext-bus-licenses-service/services/LicensesAsync");
                _endPoints.Add(HcsEndPoints.Nsi, "ext-bus-nsi-service/services/Nsi");
                _endPoints.Add(HcsEndPoints.NsiAsync, "ext-bus-nsi-service/services/NsiAsync");
                _endPoints.Add(HcsEndPoints.NsiCommon, "ext-bus-nsi-common-service/services/NsiCommon");
                _endPoints.Add(HcsEndPoints.NsiCommonAsync, "ext-bus-nsi-common-service/services/NsiCommonAsync");
                _endPoints.Add(HcsEndPoints.OrgRegistryCommon, "ext-bus-org-registry-common-service/services/OrgRegistryCommon");
                _endPoints.Add(HcsEndPoints.OrgRegistryCommonAsync, "ext-bus-org-registry-common-service/services/OrgRegistryCommonAsync");
                _endPoints.Add(HcsEndPoints.OrgRegistry, "ext-bus-org-registry-service/services/OrgRegistry");
                _endPoints.Add(HcsEndPoints.OrgRegistryAsync, "ext-bus-org-registry-service/services/OrgRegistryAsync");
                _endPoints.Add(HcsEndPoints.PaymentsAsync, "ext-bus-payment-service/services/PaymentAsync");
            }

            public static string GetPath(HcsEndPoints endPoint)
            {
                return _endPoints[endPoint];
            }
        }

        public static class UserAuth
        {
            public const string Name = "sit";
            public const string Passwd = "xw{p&&Ee3b9r8?amJv*]";
        }
    }

    /// <summary>
    /// Имена конечных точек
    /// </summary>
    public enum HcsEndPoints
    {
        OrgRegistry,
        OrgRegistryAsync,
        OrgRegistryCommon,
        OrgRegistryCommonAsync,
        NsiCommon,
        NsiCommonAsync,
        Nsi,
        NsiAsync,
        HouseManagement,
        HouseManagementAsync,
        DebtRequestsAsync,
        Bills,
        BillsAsync,
        Licenses,
        LicensesAsync,
        DeviceMetering,
        DeviceMeteringAsync,
        PaymentsAsync
    }

    /// <summary>
    /// Роли организаций в ГИС
    /// </summary>
    public enum HcsOrganizationRoles
    {
        /// <summary>
        /// УК/ТСЖ/ЖСК
        /// </summary>
        UK,

        /// <summary>
        /// Ресурсоснабжающая организация
        /// </summary>
        RSO,

        /// <summary>
        /// Расчетный центр
        /// </summary>
        RC,
    }

    public class HcsAsyncRequestStateTypes 
    {
        public const int Received = 1;
        public const int InProgress = 2;
        public const int Ready = 3;
    }
}
