// **********************************************************************
//
// Copyright (c) 2001
// ZeroC, Inc.
// Huntsville, AL, USA
//
// All Rights Reserved
//
// **********************************************************************

package IceInternal;

public final class LocatorAdapterTable
{
    LocatorAdapterTable()
    {
    }

    synchronized void
    clear()
    {
	_table.clear();
    }

    synchronized IceInternal.Endpoint[]
    get(String adapter)
    {
	return (IceInternal.Endpoint[])_table.get(adapter);
    }

    synchronized void
    add(String adapter, IceInternal.Endpoint[] endpoints)
    {
	_table.put(adapter, endpoints);
    }

    synchronized IceInternal.Endpoint[]
    remove(String adapter)
    {
	return (IceInternal.Endpoint[])_table.remove(adapter);
    }

    private java.util.HashMap _table = new java.util.HashMap();
};

