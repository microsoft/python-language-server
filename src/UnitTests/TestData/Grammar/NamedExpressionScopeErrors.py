[x for x, y in (pairs2 := pairs) if x % 2 == 0]
[x for x, y in ([1, 2, 3, pairs2 := pairs]) if x % 2 == 0]