const { execSync } = require('child_process');
const osc = require('osc');
const os = require('os');

console.log('========================================');
console.log('🌐 Network & OSC Connection Tester');
console.log('========================================\n');

// 현재 컴퓨터의 IP 주소 확인
function getLocalIPAddresses() {
    const interfaces = os.networkInterfaces();
    const addresses = [];
    
    for (const name of Object.keys(interfaces)) {
        for (const iface of interfaces[name]) {
            // IPv4이고 내부 주소가 아닌 경우
            if (iface.family === 'IPv4' && !iface.internal) {
                addresses.push({
                    name: name,
                    address: iface.address
                });
            }
        }
    }
    
    return addresses;
}

// Ping 테스트
function testPing(targetHost) {
    console.log(`\n🏓 Ping Test to ${targetHost}:`);
    try {
        const result = execSync(`ping -n 4 ${targetHost}`, { encoding: 'utf8' });
        const lines = result.split('\n');
        
        // 결과 파싱
        const successLine = lines.find(line => line.includes('ms'));
        if (successLine) {
            console.log('   ✅ Host is reachable!');
            console.log('   ' + successLine.trim());
            return true;
        } else {
            console.log('   ❌ Host is unreachable');
            return false;
        }
    } catch (error) {
        console.log('   ❌ Ping failed - Host may be unreachable or blocking ICMP');
        return false;
    }
}

// OSC 연결 테스트
function testOSCConnection(targetHost, targetPort, timeout = 5000) {
    return new Promise((resolve, reject) => {
        console.log(`\n📡 OSC Connection Test to ${targetHost}:${targetPort}:`);
        
        let udpPort;
        let timeoutId;
        let testCompleted = false;
        
        try {
            udpPort = new osc.UDPPort({
                localAddress: '0.0.0.0',
                localPort: 0,
                remoteAddress: targetHost,
                remotePort: targetPort,
                metadata: true
            });
            
            udpPort.on('ready', () => {
                console.log('   ✅ OSC port opened successfully');
                console.log('   📤 Sending test message...');
                
                // 테스트 메시지 전송
                udpPort.send({
                    address: '/test/connection',
                    args: [
                        { type: 's', value: 'Connection Test' },
                        { type: 'i', value: Date.now() }
                    ]
                });
                
                console.log('   ✅ Test message sent!');
                console.log('   💡 If TD server receives this, connection is working!');
                
                // 3초 후 종료
                setTimeout(() => {
                    if (!testCompleted) {
                        testCompleted = true;
                        udpPort.close();
                        clearTimeout(timeoutId);
                        resolve(true);
                    }
                }, 3000);
            });
            
            udpPort.on('error', (error) => {
                if (!testCompleted) {
                    testCompleted = true;
                    console.log('   ❌ OSC connection error:', error.message);
                    clearTimeout(timeoutId);
                    reject(error);
                }
            });
            
            // 타임아웃 설정
            timeoutId = setTimeout(() => {
                if (!testCompleted) {
                    testCompleted = true;
                    console.log('   ⚠️ Connection timeout');
                    if (udpPort) udpPort.close();
                    reject(new Error('Connection timeout'));
                }
            }, timeout);
            
            udpPort.open();
            
        } catch (error) {
            if (!testCompleted) {
                testCompleted = true;
                console.log('   ❌ Failed to create OSC port:', error.message);
                reject(error);
            }
        }
    });
}

// 방화벽 설정 안내
function showFirewallInstructions(port) {
    console.log('\n========================================');
    console.log('🔥 방화벽 설정이 필요한 경우');
    console.log('========================================');
    console.log('\n[TD 서버 컴퓨터에서 실행]');
    console.log('\n1. Windows 방화벽 열기:');
    console.log('   제어판 > Windows Defender 방화벽 > 고급 설정');
    console.log('\n2. 인바운드 규칙 추가:');
    console.log('   - 새 규칙 클릭');
    console.log('   - 포트 선택 > 다음');
    console.log('   - UDP 선택');
    console.log(`   - 특정 로컬 포트: ${port}`);
    console.log('   - 연결 허용');
    console.log('   - 이름: "OSC Winter Road"');
    console.log('\n3. 또는 PowerShell 명령어 (관리자 권한):');
    console.log(`   New-NetFirewallRule -DisplayName "OSC Winter Road" -Direction Inbound -Protocol UDP -LocalPort ${port} -Action Allow`);
    console.log('\n========================================\n');
}

// 메인 테스트 실행
async function runTests() {
    // 로컬 IP 주소 표시
    console.log('📍 Local IP Addresses:');
    const addresses = getLocalIPAddresses();
    if (addresses.length === 0) {
        console.log('   ⚠️ No network interfaces found');
    } else {
        addresses.forEach(addr => {
            console.log(`   - ${addr.name}: ${addr.address}`);
        });
    }
    
    // 테스트 대상 설정
    const targetHost = process.argv[2] || '192.168.0.13';
    const targetPort = parseInt(process.argv[3]) || 50013;
    
    console.log(`\n🎯 Testing connection to TD Server:`);
    console.log(`   Host: ${targetHost}`);
    console.log(`   Port: ${targetPort} (UDP)`);
    
    // 1. Ping 테스트
    const pingSuccess = testPing(targetHost);
    
    if (!pingSuccess) {
        console.log('\n⚠️ Warning: Ping failed, but OSC might still work');
        console.log('   (Some networks block ICMP ping)');
    }
    
    // 2. OSC 연결 테스트
    try {
        await testOSCConnection(targetHost, targetPort);
        
        console.log('\n========================================');
        console.log('✅ Connection Test Summary:');
        console.log('========================================');
        console.log(`✅ OSC messages can be sent to ${targetHost}:${targetPort}`);
        console.log('✅ Network connection is working!');
        console.log('\n💡 Next Steps:');
        console.log('1. Check TD server to see if it received the test message');
        console.log('2. If received, your setup is complete!');
        console.log('3. If not received, check firewall settings on TD server');
        console.log('========================================\n');
        
    } catch (error) {
        console.log('\n========================================');
        console.log('❌ Connection Test Summary:');
        console.log('========================================');
        console.log(`❌ Failed to connect to ${targetHost}:${targetPort}`);
        console.log('\n🔍 Possible Issues:');
        console.log('1. TD server is not running or not on this IP');
        console.log('2. Firewall is blocking UDP port ' + targetPort);
        console.log('3. Not on the same network');
        console.log('4. IP address is incorrect');
        
        showFirewallInstructions(targetPort);
    }
}

// 사용법 표시
if (process.argv.includes('--help') || process.argv.includes('-h')) {
    console.log('Usage:');
    console.log('  node test-network-connection.js [host] [port]');
    console.log('\nExamples:');
    console.log('  node test-network-connection.js                    # Test 192.168.0.13:50013');
    console.log('  node test-network-connection.js 192.168.0.20       # Test 192.168.0.20:50013');
    console.log('  node test-network-connection.js 192.168.0.20 7000  # Test 192.168.0.20:7000');
    console.log('\nOptions:');
    console.log('  -h, --help    Show this help message');
    process.exit(0);
}

// 테스트 실행
runTests().catch(error => {
    console.error('❌ Test error:', error);
    process.exit(1);
});







