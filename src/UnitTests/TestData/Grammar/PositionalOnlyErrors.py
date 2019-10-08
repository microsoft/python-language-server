def f(a, *b, /):
    pass

def f(a, *, b, /):
    pass

def f(a, *, b, **kwargs, /):
    pass

def f(a, **kwargs, /):
    pass

def f(a, /, b, /, c):
    pass

def f(a, /:int):
    pass

def f(a, / =123):
    pass

def f(/):
    pass
