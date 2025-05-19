import pandas as pd
import matplotlib.pyplot as plt
import os

COL_LAMBDA = 'Lambda'
COL_P0_EXP = 'P0_Эксп'
COL_P_OTK_EXP = 'P_Отк_Эксп'
COL_Q_EXP = 'Q_Эксп'
COL_A_EXP = 'A_Эксп'
COL_K_EXP = 'k_Эксп'

COL_P0_TEOR = 'P0_Теор'
COL_P_OTK_TEOR = 'P_Отк_Теор'
COL_Q_TEOR = 'Q_Теор'
COL_A_TEOR = 'A_Теор'
COL_K_TEOR = 'k_Теор'


CSV_FILE_PATH = 'Lab08/simulation_results.csv'

RESULTS_DIR = 'result'

def create_plot(df, x_col, y_exp_col, y_teor_col, title, xlabel, ylabel, filename):

    plt.figure(figsize=(10, 6))
    plt.plot(df[x_col], df[y_exp_col], marker='o', linestyle='-', label='Эксперимент')
    plt.plot(df[x_col], df[y_teor_col], marker='o', linestyle='--', label='Теория')
    
    plt.title(title)
    plt.xlabel(xlabel)
    plt.ylabel(ylabel)
    plt.legend()
    plt.grid(True)
    
    if "P0" in ylabel or "P_отк" in ylabel or "Q" in ylabel :
        plt.ylim(0, max(1.1, df[y_exp_col].max() * 1.1, df[y_teor_col].max() * 1.1) if not df[y_exp_col].empty and not df[y_teor_col].empty else 1.1) # для пределов


    if not os.path.exists(RESULTS_DIR):
        os.makedirs(RESULTS_DIR)
        
    save_path = os.path.join(RESULTS_DIR, filename)
    plt.savefig(save_path)
    print(f"График сохранен: {save_path}")
    plt.close()

def main():
    try:
        df = pd.read_csv(CSV_FILE_PATH, sep=';')
        print("CSV файл успешно загружен")
    except FileNotFoundError:
        print(f"CSV файл не найден по пути {CSV_FILE_PATH}")
        return
    except Exception as e:
        print(f"Ошибка при чтении CSV файла: {e}")
        return

    create_plot(df, COL_LAMBDA, COL_P0_EXP, COL_P0_TEOR,
                'Зависимость вероятности простоя системы (P0) от интенсивности потока (λ)',
                'Интенсивность потока заявок (λ), заявок/сек',
                'Вероятность простоя (P0)',
                'p-1.png')

    create_plot(df, COL_LAMBDA, COL_P_OTK_EXP, COL_P_OTK_TEOR,
                'Зависимость вероятности отказа (Pотк) от интенсивности потока (λ)',
                'Интенсивность потока заявок (λ), заявок/сек',
                'Вероятность отказа (Pотк)',
                'p-2.png')

    create_plot(df, COL_LAMBDA, COL_Q_EXP, COL_Q_TEOR,
                'Зависимость относительной пропускной способности (Q) от интенсивности потока (λ)',
                'Интенсивность потока заявок (λ), заявок/сек',
                'Относительная пропускная способность (Q)',
                'p-3.png')

    create_plot(df, COL_LAMBDA, COL_A_EXP, COL_A_TEOR,
                'Зависимость абсолютной пропускной способности (A) от интенсивности потока (λ)',
                'Интенсивность потока заявок (λ), заявок/сек',
                'Абсолютная пропускная способность (A), заявок/сек',
                'p-4.png')

    create_plot(df, COL_LAMBDA, COL_K_EXP, COL_K_TEOR,
                'Зависимость среднего числа занятых каналов (k) от интенсивности потока (λ)',
                'Интенсивность потока заявок (λ), заявок/сек',
                'Среднее число занятых каналов (k)',
                'p-5.png')
    
    print("\nВсе графики созданы и сохранены в папке 'result'")

if __name__ == '__main__':
    main() 