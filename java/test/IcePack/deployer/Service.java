// **********************************************************************
//
// Copyright (c) 2002
// ZeroC, Inc.
// Huntsville, AL, USA
//
// All Rights Reserved
//
// **********************************************************************

public class Service extends Ice.LocalObjectImpl implements IceBox.Service
{
    public void
    start(String name, Ice.Communicator communicator, String[] args)
        throws IceBox.FailureException
    {
	Ice.Properties properties = communicator.getProperties();

        Ice.ObjectAdapter adapter = communicator.createObjectAdapter(name);
        Ice.Object object = new TestI(adapter, communicator.getProperties());
        adapter.add(object, Ice.Util.stringToIdentity(properties.getProperty(name + ".Identity")));
        adapter.activate();
    }

    public void
    stop()
    {
    }
}
