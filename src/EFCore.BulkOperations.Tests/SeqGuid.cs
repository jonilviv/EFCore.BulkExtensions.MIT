using RT.Comb;
using System;

namespace EFCore.BulkOperations.Tests;

public static class SeqGuid
{
    private static readonly ICombProvider __sqlNoRepeatCombs = new SqlCombProvider(new SqlDateTimeStrategy(), new UtcNoRepeatTimestampProvider().GetTimestamp);

    public static Guid Create()
    {
        return __sqlNoRepeatCombs.Create();
    }
}