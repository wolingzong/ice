//
// Copyright (c) ZeroC, Inc. All rights reserved.
//

namespace IceInternal
{
    public interface IResponseHandler
    {
        void sendResponse(int requestId, Ice.OutputStream os, byte status, bool amd);
        void sendNoResponse();
        bool systemException(int requestId, Ice.SystemException ex, bool amd);
        void invokeException(int requestId, Ice.LocalException ex, int invokeNum, bool amd);
    }
}