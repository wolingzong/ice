// **********************************************************************
//
// Copyright (c) 2001
// ZeroC, Inc.
// Huntsville, AL, USA
//
// All Rights Reserved
//
// **********************************************************************

#ifndef ICE_ROUTER_INFO_H
#define ICE_ROUTER_INFO_H

#include <IceUtil/Shared.h>
#include <IceUtil/Mutex.h>
#include <Ice/RouterInfoF.h>
#include <Ice/RouterF.h>
#include <Ice/ProxyF.h>
#include <Ice/RoutingTableF.h>

namespace IceInternal
{

class RouterManager : public ::IceUtil::Shared, public ::IceUtil::Mutex
{
public:

    RouterManager();

    void destroy();

    //
    // Returns router info for a given router. Automatically creates
    // the router info if it doesn't exist yet.
    //
    RouterInfoPtr get(const ::Ice::RouterPrx&);

private:

    std::map< ::Ice::RouterPrx, RouterInfoPtr> _table;
    std::map< ::Ice::RouterPrx, RouterInfoPtr>::iterator _tableHint;
};

class RouterInfo : public ::IceUtil::Shared, public ::IceUtil::Mutex
{
public:

    RouterInfo(const ::Ice::RouterPrx&);

    void destroy();

    bool operator==(const RouterInfo&) const;
    bool operator!=(const RouterInfo&) const;
    bool operator<(const RouterInfo&) const;

    ::Ice::RouterPrx getRouter() const;
    ::Ice::ObjectPrx getClientProxy();
    void setClientProxy(const ::Ice::ObjectPrx&);
    ::Ice::ObjectPrx getServerProxy();
    void setServerProxy(const ::Ice::ObjectPrx&);
    void addProxy(const ::Ice::ObjectPrx&);
    void setAdapter(const ::Ice::ObjectAdapterPtr&);
    ::Ice::ObjectAdapterPtr getAdapter() const;

private:

    ::Ice::RouterPrx _router; // Immutable.
    ::Ice::ObjectPrx _clientProxy;
    ::Ice::ObjectPrx _serverProxy;
    RoutingTablePtr _routingTable; // Immutable.
    ::Ice::ObjectAdapterPtr _adapter;
};

}

#endif
