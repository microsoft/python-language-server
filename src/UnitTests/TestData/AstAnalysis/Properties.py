class A(object):
    def methodA(self):
        return 's'

class B(object):
    def getA(self):
        return self.propA

    @property
    def propA(self):
        return A()
