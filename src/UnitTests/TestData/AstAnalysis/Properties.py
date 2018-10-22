class A(object):
    def methodA(self):
        return 's'

class B(object):
    def getA(self):
        return self.propA()

    def propA(self):
        return A()
