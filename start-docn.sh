#!/bin/bash
# ============================================================================
# DocN - Document Archive System Startup Script (Linux/macOS)
# ============================================================================
# This script starts both the Server (Backend API) and Client (Frontend UI)
# ============================================================================

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo ""
echo -e "${CYAN}============================================================================${NC}"
echo -e "${CYAN}   DocN - Document Archive System${NC}"
echo -e "${CYAN}============================================================================${NC}"
echo ""
echo -e "${YELLOW}Starting the DocN System...${NC}"
echo ""
echo -e "${NC}This will start:${NC}"
echo -e "${NC}  1. DocN.Server (Backend API) on https://localhost:5211${NC}"
echo -e "${NC}  2. DocN.Client (Frontend UI) on http://localhost:5036${NC}"
echo ""
echo -e "${RED}Press Ctrl+C to stop both applications${NC}"
echo ""
echo -e "${CYAN}============================================================================${NC}"
echo ""

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}ERROR: .NET SDK is not installed or not in PATH${NC}"
    echo -e "${RED}Please install .NET 10.0 SDK from https://dotnet.microsoft.com/download${NC}"
    exit 1
fi

# Cleanup function
cleanup() {
    echo ""
    echo -e "${YELLOW}Shutting down applications...${NC}"
    kill $SERVER_PID 2>/dev/null
    kill $CLIENT_PID 2>/dev/null
    echo -e "${GREEN}Applications stopped.${NC}"
    exit 0
}

# Register cleanup on SIGINT (Ctrl+C)
trap cleanup SIGINT

# Start the Server
echo -e "${GREEN}[1/2] Starting DocN.Server (Backend API)...${NC}"
cd DocN.Server
dotnet run &
SERVER_PID=$!
cd ..

# Wait for Server to initialize
echo -e "${YELLOW}Waiting for Server to initialize...${NC}"
sleep 10

# Start the Client
echo -e "${GREEN}[2/2] Starting DocN.Client (Frontend UI)...${NC}"
cd DocN.Client
dotnet run &
CLIENT_PID=$!
cd ..

echo ""
echo -e "${CYAN}============================================================================${NC}"
echo -e "${GREEN} APPLICATIONS STARTED!${NC}"
echo -e "${CYAN}============================================================================${NC}"
echo ""
echo -e "${NC}  Server (API):   https://localhost:5211${NC}"
echo -e "${NC}  Client (UI):    http://localhost:5036${NC}"
echo ""
echo -e "${NC}  Open your browser to: ${CYAN}http://localhost:5036${NC}"
echo ""
echo -e "${YELLOW}  Press Ctrl+C to stop both applications${NC}"
echo ""
echo -e "${CYAN}============================================================================${NC}"
echo ""

# Wait for both processes
wait $SERVER_PID $CLIENT_PID
