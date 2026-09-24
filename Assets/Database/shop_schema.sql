
--  상점 시스템 MySQL Schema


-- 상품 기본 정보
CREATE TABLE shop_items (
    item_id VARCHAR(20) PRIMARY KEY,
    name VARCHAR(50) NOT NULL,
    item_type VARCHAR(20) NOT NULL,
    price BIGINT NOT NULL,
    purchase_limit INT NOT NULL DEFAULT 0,
    description VARCHAR(255)
)
DEFAULT CHARSET=utf8mb4
COLLATE=utf8mb4_unicode_ci;


-- 상품별 효과
CREATE TABLE shop_item_effects (
    effect_id INT AUTO_INCREMENT PRIMARY KEY,
    item_id VARCHAR(20) NOT NULL,
    effect_type VARCHAR(50) NOT NULL,
    effect_value DECIMAL(12,4) NOT NULL,
    effect_trigger VARCHAR(20) NOT NULL,

    FOREIGN KEY (item_id)
        REFERENCES shop_items(item_id)
        ON DELETE CASCADE
)
DEFAULT CHARSET=utf8mb4
COLLATE=utf8mb4_unicode_ci;


-- ============================================
-- 상점 기본 상품 데이터
--
-- purchase_limit = 0 : 구매 제한 없음
-- purchase_limit = 1 : 한 번만 구매 가능
-- ============================================

INSERT INTO shop_items (
    item_id,
    name,
    item_type,
    price,
    purchase_limit,
    description
)
VALUES
(
    'SHOP_001',
    '커피 마시기',
    'CONSUMABLE',
    30000,
    0,
    '커피를 마셔 피로도를 3 회복한다.'
),
(
    'SHOP_002',
    '외식하기',
    'CONSUMABLE',
    30000,
    0,
    'AP 10을 사용하고 피로도를 5 회복한다.'
),
(
    'SHOP_003',
    '영화 관람',
    'CONSUMABLE',
    100000,
    0,
    '영화를 관람하여 피로도를 15 회복한다.'
),
(
    'SHOP_004',
    '놀이공원 가기',
    'CONSUMABLE',
    100000,
    0,
    'AP 20을 사용하고 피로도를 25 회복한다.'
),
(
    'SHOP_005',
    '해외여행 가기',
    'CONSUMABLE',
    5000000,
    0,
    'AP를 50 회복하고 피로도를 100 회복한다.'
),
(
    'SHOP_006',
    '휴대폰',
    'PERMANENT',
    10000000,
    1,
    '보유 시 하급 정보를 무료로 이용할 수 있다.'
),
(
    'SHOP_007',
    '명품',
    'PERMANENT',
    20000000,
    1,
    '보유 시 매 턴 피로도를 10 회복한다.'
),
(
    'SHOP_008',
    '노트북',
    'PERMANENT',
    30000000,
    1,
    '투자 수익에 5% 보너스를 적용하고 매 턴 직급 경험치를 10 지급한다.'
),
(
    'SHOP_009',
    '자동차',
    'PERMANENT',
    50000000,
    1,
    '구매 시 최대 AP와 최대 피로도가 각각 20 증가한다.'
),
(
    'SHOP_010',
    '집',
    'PERMANENT',
    1000000000,
    1,
    '보유 시 해피엔딩 조건을 충족한다.'
);


-- ============================================
-- 상품 효과 데이터
-- ============================================

INSERT INTO shop_item_effects (
    item_id,
    effect_type,
    effect_value,
    effect_trigger
)
VALUES

-- 커피
('SHOP_001', 'FATIGUE', -3, 'IMMEDIATE'),

-- 외식
('SHOP_002', 'AP', -10, 'IMMEDIATE'),
('SHOP_002', 'FATIGUE', -5, 'IMMEDIATE'),

-- 영화
('SHOP_003', 'FATIGUE', -15, 'IMMEDIATE'),

-- 놀이공원
('SHOP_004', 'AP', -20, 'IMMEDIATE'),
('SHOP_004', 'FATIGUE', -25, 'IMMEDIATE'),

-- 해외여행
('SHOP_005', 'AP', 50, 'IMMEDIATE'),
('SHOP_005', 'FATIGUE', -100, 'IMMEDIATE'),

-- 휴대폰
('SHOP_006', 'FREE_LOW_GRADE_INFO', 1, 'PERMANENT'),

-- 명품
('SHOP_007', 'FATIGUE', -10, 'PER_TURN'),

-- 노트북
('SHOP_008', 'INVESTMENT_RETURN_BONUS', 0.05, 'PERMANENT'),
('SHOP_008', 'CAREER_EXP', 10, 'PER_TURN'),

-- 자동차
('SHOP_009', 'MAX_AP', 20, 'ON_PURCHASE'),
('SHOP_009', 'MAX_FATIGUE', 20, 'ON_PURCHASE'),

-- 집
('SHOP_010', 'HAPPY_ENDING_CONDITION', 1, 'PERMANENT');