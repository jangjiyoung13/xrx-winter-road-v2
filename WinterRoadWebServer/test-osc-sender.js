const osc = require('osc');

console.log('========================================');
console.log('🚀 OSC Test Sender');
console.log('========================================\n');

// 설정
const TARGET_HOST = '127.0.0.1';  // 로컬 테스트용 (같은 컴퓨터)
const TARGET_PORT = 50013;

// OSC UDP 포트 생성 (송신용)
const udpPort = new osc.UDPPort({
    localAddress: '0.0.0.0',
    localPort: 0,  // 랜덤 포트 사용
    remoteAddress: TARGET_HOST,
    remotePort: TARGET_PORT,
    metadata: true
});

// 준비 완료
udpPort.on('ready', () => {
    console.log(`✅ OSC Sender is ready!`);
    console.log(`   Target: ${TARGET_HOST}:${TARGET_PORT}\n`);
    
    // 테스트 메시지 전송
    runTests();
});

// 에러 처리
udpPort.on('error', (error) => {
    console.error('❌ OSC Sender Error:', error.message);
});

// UDP 포트 열기
try {
    udpPort.open();
} catch (error) {
    console.error('❌ Failed to open UDP port:', error.message);
    process.exit(1);
}

// 테스트 시나리오
async function runTests() {
    console.log('🧪 Starting OSC Test Sequence...\n');
    
    // Test 1: Winter Road Start
    await delay(1000);
    console.log('📤 Test 1: Sending /goto_live (Winter Road Start)');
    udpPort.send({
        address: '/goto_live',
        args: []
    });
    console.log('   ✅ Sent!\n');
    
    // Test 2: 5초 대기
    await delay(5000);
    console.log('⏳ Waiting 5 seconds...\n');
    
    // Test 3: Winter Road End
    console.log('📤 Test 2: Sending /goto_preset (Winter Road End)');
    udpPort.send({
        address: '/goto_preset',
        args: []
    });
    console.log('   ✅ Sent!\n');
    
    // Test 4: 커스텀 메시지 (인자 포함)
    await delay(2000);
    console.log('📤 Test 3: Sending custom message with arguments');
    udpPort.send({
        address: '/test/custom',
        args: [
            { type: 'i', value: 42 },        // 정수
            { type: 'f', value: 3.14 },      // 실수
            { type: 's', value: 'Hello!' }   // 문자열
        ]
    });
    console.log('   ✅ Sent!\n');
    
    // 완료
    await delay(1000);
    console.log('✅ All tests completed!');
    console.log('👋 Closing sender...\n');
    
    udpPort.close();
    process.exit(0);
}

// 지연 함수
function delay(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

// Graceful shutdown
process.on('SIGINT', () => {
    console.log('\n\n👋 Shutting down OSC Sender...');
    udpPort.close();
    process.exit(0);
});







