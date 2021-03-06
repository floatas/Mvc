// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.Framework.Internal;

namespace Microsoft.AspNet.Mvc
{
    public interface IExceptionFilter : IFilterMetadata
    {
        void OnException([NotNull] ExceptionContext context);
    }
}
