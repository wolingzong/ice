// **********************************************************************
//
// Copyright (c) 2001
// ZeroC, Inc.
// Huntsville, AL, USA
//
// All Rights Reserved
//
// **********************************************************************

class LibraryI extends _LibraryDisp
{
    public synchronized BookPrx
    createBook(BookDescription description, Ice.Current current)
	throws DatabaseException, BookExistsException
    {
	BookPrx book = isbnToBook(description.isbn);

	try
	{
	    book.ice_ping();

	    //
	    // The book already exists.
	    //
	    throw new BookExistsException();
	}
	catch(Ice.ObjectNotExistException e)
	{
	    //
	    // Book doesn't exist, ignore the exception.
	    //
	}

	//
	// Create a new book Servant.
	//
	BookI bookI = new BookI(this);
	bookI.description = description;

	Ice.Identity ident = createBookIdentity(description.isbn);

	//
	// Create a new Ice Object in the evictor, using the new
	// identity and the new Servant.
	//
	// This can throw EvictorDeactivatedException (which indicates
	// an internal error). The exception is currently ignored.
	//
	_evictor.createObject(ident, bookI);

	try
	{
	    //
	    // Add the isbn number to the authors map.
	    //
	    String[] isbnSeq = (String[])_authors.get(description.authors);
	    int length = (isbnSeq == null) ? 0 : isbnSeq.length;
	    String[] newIsbnSeq = new String[length+1];

	    if(isbnSeq != null)
	    {
		System.arraycopy(isbnSeq, 0, newIsbnSeq, 0, length);
	    }
	    newIsbnSeq[length] = description.isbn;

	    _authors.fastPut(description.authors, newIsbnSeq);
	
	    return book;
	}
	catch(Freeze.DBException ex)
	{
	    DatabaseException e = new DatabaseException();
	    e.message = ex.message;
	    throw e;
	}
    }

    //
    // No locking is necessary since no internal mutable state is
    // accessed.
    //
    public BookPrx
    findByIsbn(String isbn, Ice.Current current)
	throws DatabaseException
    {
	try
	{
	    BookPrx book = isbnToBook(isbn);
	    book.ice_ping();

	    return book;
	}
	catch(Ice.ObjectNotExistException ex)
	{
	    //
	    // Book doesn't exist, return a null proxy.
	    //
	    return null;
	}
    }

    public synchronized BookPrx[]
    findByAuthors(String authors, Ice.Current current)
	throws DatabaseException
    {
	try
	{
	    //
	    // Lookup all books that match the given authors, and
	    // return them to the caller.
	    //
	    String[] isbnSeq = (String[])_authors.get(authors);

	    int length = (isbnSeq == null) ? 0 : isbnSeq.length;
	    BookPrx[] books = new BookPrx[length];

	    if(isbnSeq != null)
	    {
		for(int i = 0; i < length; ++i)
		{
		    books[i] = isbnToBook(isbnSeq[i]);
		}
	    }

	    return books;
	}
	catch(Freeze.DBException ex)
	{
	    DatabaseException e = new DatabaseException();
	    e.message = ex.message;
	    throw e;
	}
    }

    public void
    setEvictorSize(int size, Ice.Current current)
	throws DatabaseException
    {
	//
	// No synchronization necessary, _evictor is immutable.
	//
	_evictor.setSize(size);
    }

    public void
    shutdown(Ice.Current current)
    {
	//
	// No synchronization necessary, _adapter is immutable.
	//
	_adapter.getCommunicator().shutdown();
    }

    protected synchronized void
    remove(BookDescription description)
	throws DatabaseException
    {
	try
	{
	    String[] isbnSeq = (String[])_authors.get(description.authors);

	    //
	    // If the title isn't found then raise a record not found
	    // exception.
	    //
	    if(isbnSeq == null)
	    {
		throw new Freeze.DBNotFoundException();
	    }

	    int i;
	    for(i = 0; i < isbnSeq.length; ++i)
	    {
		if(isbnSeq[i].equals(description.isbn))
		{
		    break;
		}
	    }

	    if(i >= isbnSeq.length)
	    {
		throw new Freeze.DBNotFoundException();
	    }

	    if(isbnSeq.length == 1)
	    {
		//
		// If there are no further associated isbn numbers then remove
		// the record.
		//
		_authors.fastRemove(description.authors);
	    }
	    else
	    {
		//
		// Remove the isbn number from the sequence and write
		// back the new record.
		//
		String[] newIsbnSeq = new String[isbnSeq.length-1];
		System.arraycopy(isbnSeq, 0, newIsbnSeq, 0, i);
		if(i < newIsbnSeq.length - 1)
		{
		    System.arraycopy(isbnSeq, i+1, newIsbnSeq, i, isbnSeq.length - i - 1);
		}
	    
		_authors.fastPut(description.authors, newIsbnSeq);
	    }

	    //
	    // This can throw EvictorDeactivatedException (which
	    // indicates an internal error). The exception is
	    // currently ignored.
	    //
	    _evictor.destroyObject(createBookIdentity(description.isbn));
	}
	catch(Freeze.DBException ex)
	{
	    DatabaseException e = new DatabaseException();
	    e.message = ex.message;
	    throw e;
	}
    }

    LibraryI(Ice.ObjectAdapter adapter, Freeze.DB db, Freeze.Evictor evictor)
    {
	_adapter = adapter;
	_evictor = evictor;
	_authors = new StringIsbnSeqDict(db);
    }

    private Ice.Identity
    createBookIdentity(String isbn)
    {
	//
	// Note that the identity category is important since the
	// locator was installed for the category 'book'.
	//
	Ice.Identity ident = new Ice.Identity();
	ident.category = "book";
	ident.name = isbn;

	return ident;
    }

    private BookPrx
    isbnToBook(String isbn)
    {
	return BookPrxHelper.uncheckedCast(_adapter.createProxy(createBookIdentity(isbn)));
    }

    private Ice.ObjectAdapter _adapter;
    private Freeze.Evictor _evictor;
    private StringIsbnSeqDict _authors;
}
