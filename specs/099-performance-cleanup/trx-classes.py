"""Aggregate a .trx file per test class (and per xUnit collection).

Usage: python -I trx-classes.py <file.trx> [top-n] [collection-map.json]
The collection map is {"ClassName": "CollectionName"}; classes without an entry count as "(parallel)".
"""
import collections
import json
import sys
import xml.etree.ElementTree as ET

NS = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}


def secs(d):
    h, m, s = d.split(":")
    return int(h) * 3600 + int(m) * 60 + float(s)


root = ET.parse(sys.argv[1]).getroot()
top = int(sys.argv[2]) if len(sys.argv) > 2 else 20
coll = json.load(open(sys.argv[3])) if len(sys.argv) > 3 else {}
classes = {ut.get("id"): ut.find("t:TestMethod", NS).get("className") for ut in root.iterfind(".//t:UnitTest", NS)}
total = collections.defaultdict(float)
count = collections.Counter()
by_coll = collections.defaultdict(float)
for res in root.iterfind(".//t:UnitTestResult", NS):
    c = classes[res.get("testId")]
    d = secs(res.get("duration", "0:0:0"))
    total[c] += d
    count[c] += 1
    by_coll[coll.get(c.split(".")[-1], "(parallel)")] += d
print("sum of all test durations: %.0f s, classes %d, tests %d" % (sum(total.values()), len(total), sum(count.values())))
for c, t in sorted(total.items(), key=lambda x: -x[1])[:top]:
    print("%7.1f s  %4d  %s" % (t, count[c], c.replace("Spectra.CLI.Tests.", "")))
if coll:
    for k, v in sorted(by_coll.items(), key=lambda x: -x[1]):
        print("%7.1f s  %s" % (v, k))
