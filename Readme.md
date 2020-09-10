# TLS Connect stress test

This app makes many janky connections to https://127.0.0.1:443 to try to freak out any code that accepts TLS connections.

Usage: TlsConnectStressTest [-Load 1...N]
Where
  Load - Relative amount of load to generate (in the present implementation, sets the number of load generator threads)

What does it do:
* Connect over and over and over again to the target
* Once connected, send a big batch of random bytes
* Chop up the data stream into many tiny packets
* Ignore the server certificate

This only works on the loopback adapter, by design (flaw), because otherwise it runs out of TCP ports to connect from due to TIME_WAIT.