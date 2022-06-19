using System;
using System.Collections.Generic;
using System.Reactive.Linq;

namespace FormValidationExample.Services
{
    public class UserRegistrationService : IUserRegistrationService
    {
        private bool lastResult = true;
        private readonly IDictionary<string, bool> resultCache = new Dictionary<string, bool>();

        public IObservable<bool> IsUserNameAvailable(string userName)
        {

            if (!resultCache.TryGetValue(userName, out bool isNameAvailable))
            {
                isNameAvailable = lastResult = !lastResult;
                resultCache.Add(userName, isNameAvailable);
            }

            // Return random result with a delay to simulate server communication
            return Observable.Return(isNameAvailable).Delay(TimeSpan.FromMilliseconds(500));
        }
    }
}