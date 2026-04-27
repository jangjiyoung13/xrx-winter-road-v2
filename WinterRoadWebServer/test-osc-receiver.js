const osc = require('osc');

console.log('========================================');
console.log('🎵 OSC Test Receiver Server');
console.log('========================================');

// OSC UDP 포트 생성 (수신용)
const udpPort = new osc.UDPPort({
    localAddress: '0.0.0.0',  // 모든 네트워크 인터페이스에서 수신
    localPort: 50013,         // TD 서버와 동일한 포트 사용
    metadata: true
});

// OSC 메시지 수신 이벤트
udpPort.on('message', (oscMessage, timeTag, info) => {
    console.log('\n📨 OSC Message Received:');
    console.log('   ├─ Address:', oscMessage.address);
    console.log('   ├─ Args:', oscMessage.args);
    console.log('   ├─ From:', info.address + ':' + info.port);
    console.log('   └─ Time:', new Date().toLocaleTimeString());
    
    // 특정 메시지에 대한 상세 로그
    if (oscMessage.address === '/goto_live') {
        console.log('   🎮 [ACTION] Winter Road START triggered!');
    } else if (oscMessage.address === '/goto_preset') {
        console.log('   🏁 [ACTION] Winter Road END triggered!');
    }
});

// 준비 완료
udpPort.on('ready', () => {
    const localAddress = udpPort.options.localAddress;
    const localPort = udpPort.options.localPort;
    
    console.log('\n✅ OSC Receiver is ready!');
    console.log('   Listening on:', localAddress + ':' + localPort);
    console.log('\n📍 Test Configuration:');
    console.log('   - Local Test: 127.0.0.1:50013');
    console.log('   - Same Network: [Your-IP]:50013');
    console.log('\n⏳ Waiting for OSC messages...\n');
});

// 에러 처리
udpPort.on('error', (error) => {
    console.error('\n❌ OSC Receiver Error:');
    console.error('   ', error.message);
    
    if (error.code === 'EADDRINUSE') {
        console.error('\n💡 Port 50013 is already in use!');
        console.error('   Solution:');
        console.error('   1. Stop the existing process on port 50013');
        console.error('   2. Or change the port in this file and server.js');
    }
});

// UDP 포트 열기
try {
    udpPort.open();
} catch (error) {
    console.error('❌ Failed to open UDP port:', error.message);
    process.exit(1);
}

// Graceful shutdown
process.on('SIGINT', () => {
    console.log('\n\n👋 Shutting down OSC Receiver...');
    udpPort.close();
    console.log('✅ OSC Receiver stopped.');
    process.exit(0);
});

// 10초마다 상태 체크 (선택사항)
let messageCount = 0;
udpPort.on('message', () => {
    messageCount++;
});

setInterval(() => {
    if (messageCount === 0) {
        console.log('⏳ Still waiting... (No messages received yet)');
    }
}, 10000);







