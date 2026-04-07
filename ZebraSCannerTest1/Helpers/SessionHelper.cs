using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZebraSCannerTest1.Helpers;

public static class SessionHelper
{
    public static async Task<int> GetCurrentUserIdAsync()
    {
        var value = await SecureStorage.GetAsync("user_id");

        if (string.IsNullOrWhiteSpace(value) || !int.TryParse(value, out var userId))
            throw new InvalidOperationException("No logged in user found.");

        return userId;
    }
}