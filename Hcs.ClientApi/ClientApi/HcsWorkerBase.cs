
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Hcs.ClientApi.Config;
using Hcs.ClientApi.Interfaces;

namespace Hcs.ClientApi
{
    public abstract class HcsWorkerBase
    {
        public HcsClientConfig ClientConfig { get; private set; }

        public HcsWorkerBase(HcsClientConfig config)
        {
            this.ClientConfig = config;
        }

        public void Log(string message) => ClientConfig.Log(message);

        public Guid ParseGuid(string guid)
        {
            try {
                return Guid.Parse(guid);
            }
            catch (Exception e) {
                throw new HcsException($"Невозможно прочитать GUID из строки [{guid}]", e);
            }
        }

        public async Task<T> RunRepeatableTaskAsync<T>(
            Func<Task<T>> taskFunc, Func<Exception, bool> canIgnoreFunc, int maxAttempts)
        {
            for (int attempts = 1; ; attempts++) {
                try {
                    return await taskFunc();
                }
                catch (Exception e) {
                    if (canIgnoreFunc(e)) {
                        if (attempts < maxAttempts) {
                            Log($"Игнорирую {attempts} из {maxAttempts} допустимых ошибок");
                            continue;
                        }
                        throw new HcsException(
                            $"Более {maxAttempts} продолжений после допустимых ошибок", e);
                    }
                    throw new HcsException("Вложенная ошибка", e);
                }
            }
        }
    }
}
