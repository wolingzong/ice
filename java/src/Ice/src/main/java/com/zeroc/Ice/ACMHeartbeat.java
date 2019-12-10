//
// Copyright (c) ZeroC, Inc. All rights reserved.
//

package com.zeroc.Ice;

/**
 * Specifies the heartbeat semantics for Active Connection Management.
 **/
public enum ACMHeartbeat
{
    /**
     * Disables heartbeats.
     **/
    HeartbeatOff(0),
    /**
     * Send a heartbeat at regular intervals if the connection is idle and only if there are pending dispatch.
     **/
    HeartbeatOnDispatch(1),
    /**
     * Send a heartbeat at regular intervals when the connection is idle.
     **/
    HeartbeatOnIdle(2),
    /**
     * Send a heartbeat at regular intervals until the connection is closed.
     **/
    HeartbeatAlways(3);

    public int value()
    {
        return _value;
    }

    public static ACMHeartbeat valueOf(int v)
    {
        switch(v)
        {
        case 0:
            return HeartbeatOff;
        case 1:
            return HeartbeatOnDispatch;
        case 2:
            return HeartbeatOnIdle;
        case 3:
            return HeartbeatAlways;
        }
        return null;
    }

    private ACMHeartbeat(int v)
    {
        _value = v;
    }

    private final int _value;
}
