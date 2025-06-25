package temporal

import "net"

// getAvailablePort finds an available port by attempting to listen on port 0
// When you listen on port 0, the OS automatically assigns an available port
func getAvailablePort() (int, error) {
	listener, err := net.Listen("tcp", ":0")
	if err != nil {
		return 0, err
	}
	defer listener.Close()

	addr := listener.Addr().(*net.TCPAddr)
	return addr.Port, nil
}
